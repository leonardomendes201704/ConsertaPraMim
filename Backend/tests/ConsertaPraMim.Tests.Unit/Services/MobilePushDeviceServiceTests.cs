using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class MobilePushDeviceServiceTests
{
    private readonly Mock<IMobilePushDeviceRepository> _mobilePushDeviceRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<ILogger<MobilePushDeviceService>> _loggerMock;
    private readonly MobilePushDeviceService _service;

    public MobilePushDeviceServiceTests()
    {
        _mobilePushDeviceRepositoryMock = new Mock<IMobilePushDeviceRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _loggerMock = new Mock<ILogger<MobilePushDeviceService>>();

        _service = new MobilePushDeviceService(
            _mobilePushDeviceRepositoryMock.Object,
            _userRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact(DisplayName = "Push device service | Register | Deve criar novo registro com installationId informado")]
    public async Task RegisterAsync_ShouldCreateDevice_WhenTokenAndInstallationAreNew()
    {
        var userId = Guid.NewGuid();
        var createdDevice = default(MobilePushDevice);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync(new User { Id = userId, Role = UserRole.Client, IsActive = true });

        _mobilePushDeviceRepositoryMock
            .Setup(r => r.GetByTokenAndAppKindAsync("token-abc", "client", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MobilePushDevice?)null);
        _mobilePushDeviceRepositoryMock
            .Setup(r => r.GetByInstallationIdAndAppKindAsync("install-001", "client", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MobilePushDevice?)null);
        _mobilePushDeviceRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<MobilePushDevice>(), It.IsAny<CancellationToken>()))
            .Callback<MobilePushDevice, CancellationToken>((device, _) => createdDevice = device)
            .Returns(Task.CompletedTask);
        _mobilePushDeviceRepositoryMock
            .Setup(r => r.DeactivateByUserAndInstallationIdExceptIdAsync(
                userId,
                "client",
                "install-001",
                It.IsAny<Guid>(),
                "installation_replaced",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var response = await _service.RegisterAsync(
            userId,
            "client",
            new MobilePushDeviceRegisterRequestDto
            {
                Token = "token-abc",
                Platform = "android",
                InstallationId = "install-001",
                DeviceModel = "Pixel"
            });

        Assert.NotNull(createdDevice);
        Assert.Equal("install-001", createdDevice!.InstallationId);
        Assert.Equal("token-abc", createdDevice.Token);
        Assert.True(createdDevice.IsActive);
        Assert.Equal(response.DeviceId, createdDevice.Id);
        _mobilePushDeviceRepositoryMock.Verify(r => r.AddAsync(It.IsAny<MobilePushDevice>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Push device service | Register | Deve atualizar token quando installationId ja existe")]
    public async Task RegisterAsync_ShouldRotateToken_WhenInstallationAlreadyExists()
    {
        var userId = Guid.NewGuid();
        var existingDevice = new MobilePushDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = "token-old",
            AppKind = "client",
            Platform = "android",
            InstallationId = "install-xyz",
            IsActive = true
        };

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync(new User { Id = userId, Role = UserRole.Client, IsActive = true });

        _mobilePushDeviceRepositoryMock
            .Setup(r => r.GetByTokenAndAppKindAsync("token-new", "client", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MobilePushDevice?)null);
        _mobilePushDeviceRepositoryMock
            .Setup(r => r.GetByInstallationIdAndAppKindAsync("install-xyz", "client", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDevice);
        _mobilePushDeviceRepositoryMock
            .Setup(r => r.UpdateAsync(existingDevice, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mobilePushDeviceRepositoryMock
            .Setup(r => r.DeactivateByUserAndInstallationIdExceptIdAsync(
                userId,
                "client",
                "install-xyz",
                existingDevice.Id,
                "installation_replaced",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await _service.RegisterAsync(
            userId,
            "client",
            new MobilePushDeviceRegisterRequestDto
            {
                Token = "token-new",
                Platform = "android",
                InstallationId = "install-xyz"
            });

        Assert.Equal("token-new", existingDevice.Token);
        Assert.Equal("install-xyz", existingDevice.InstallationId);
        Assert.True(existingDevice.IsActive);
        Assert.Null(existingDevice.RevokedAtUtc);
        _mobilePushDeviceRepositoryMock.Verify(r => r.AddAsync(It.IsAny<MobilePushDevice>(), It.IsAny<CancellationToken>()), Times.Never);
        _mobilePushDeviceRepositoryMock.Verify(r => r.UpdateAsync(existingDevice, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Push device service | Register | Deve realocar token para usuario atual quando token pertencia a outro usuario")]
    public async Task RegisterAsync_ShouldReassignToken_WhenTokenWasLinkedToAnotherUser()
    {
        var oldUserId = Guid.NewGuid();
        var newUserId = Guid.NewGuid();
        var existingByToken = new MobilePushDevice
        {
            Id = Guid.NewGuid(),
            UserId = oldUserId,
            Token = "token-shared",
            AppKind = "admin",
            Platform = "android",
            InstallationId = "install-old",
            IsActive = true
        };

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(newUserId))
            .ReturnsAsync(new User { Id = newUserId, Role = UserRole.Admin, IsActive = true });

        _mobilePushDeviceRepositoryMock
            .Setup(r => r.GetByTokenAndAppKindAsync("token-shared", "admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingByToken);
        _mobilePushDeviceRepositoryMock
            .Setup(r => r.GetByInstallationIdAndAppKindAsync("install-admin", "admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MobilePushDevice?)null);
        _mobilePushDeviceRepositoryMock
            .Setup(r => r.UpdateAsync(existingByToken, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mobilePushDeviceRepositoryMock
            .Setup(r => r.DeactivateByUserAndInstallationIdExceptIdAsync(
                newUserId,
                "admin",
                "install-admin",
                existingByToken.Id,
                "installation_replaced",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await _service.RegisterAsync(
            newUserId,
            "admin",
            new MobilePushDeviceRegisterRequestDto
            {
                Token = "token-shared",
                Platform = "android",
                InstallationId = "install-admin"
            });

        Assert.Equal(newUserId, existingByToken.UserId);
        Assert.Equal("install-admin", existingByToken.InstallationId);
        Assert.True(existingByToken.IsActive);
    }

    [Fact(DisplayName = "Push device service | Unregister | Deve desativar por token e installationId")]
    public async Task UnregisterAsync_ShouldDeactivateByTokenAndInstallation()
    {
        var userId = Guid.NewGuid();

        _mobilePushDeviceRepositoryMock
            .Setup(r => r.DeactivateByUserAndTokenAsync(
                userId,
                "provider",
                "token-provider",
                "user_logout",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _mobilePushDeviceRepositoryMock
            .Setup(r => r.DeactivateByUserAndInstallationIdAsync(
                userId,
                "provider",
                "install-provider",
                "user_logout",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var response = await _service.UnregisterAsync(
            userId,
            "provider",
            new MobilePushDeviceUnregisterRequestDto
            {
                Token = "token-provider",
                InstallationId = "install-provider"
            });

        Assert.Equal(2, response.DeactivatedDevices);
        Assert.Equal("provider", response.AppKind);
        _mobilePushDeviceRepositoryMock.Verify(r => r.DeactivateByUserAndDeviceIdAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
