using Moq;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Enums;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Services;

public class ProfileServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IPlanGovernanceService> _planGovernanceServiceMock;
    private readonly ProfileService _service;

    public ProfileServiceTests()
    {
        _userRepoMock = new Mock<IUserRepository>();
        _planGovernanceServiceMock = new Mock<IPlanGovernanceService>();

        _planGovernanceServiceMock
            .Setup(s => s.GetOperationalRulesAsync(It.IsAny<ProviderPlan>()))
            .ReturnsAsync(new ProviderOperationalPlanRulesDto(
                ProviderPlan.Bronze,
                60,
                7,
                Enum.GetValues(typeof(ServiceCategory)).Cast<ServiceCategory>().ToList()));

        _planGovernanceServiceMock
            .Setup(s => s.ValidateOperationalSelectionAsync(
                It.IsAny<ProviderPlan>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyCollection<ServiceCategory>>()))
            .ReturnsAsync(new ProviderOperationalValidationResultDto(true));

        _service = new ProfileService(_userRepoMock.Object, _planGovernanceServiceMock.Object);
    }

    [Fact]
    public async Task GetProfileAsync_ShouldReturnProfile_WhenUserExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Name = "Test", Email = "a@b.com", Role = UserRole.Client };
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        // Act
        var result = await _service.GetProfileAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Name, result.Name);
    }

    [Fact]
    public async Task UpdateProviderProfileAsync_ShouldUpdate_WhenUserIsProvider()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Role = UserRole.Provider, ProviderProfile = new ProviderProfile() };
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var dto = new UpdateProviderProfileDto(20.0, "01001-000", -23.5, -46.6, new List<ServiceCategory> { ServiceCategory.Masonry });

        // Act
        var result = await _service.UpdateProviderProfileAsync(userId, dto);

        // Assert
        Assert.True(result);
        Assert.Equal(20.0, user.ProviderProfile.RadiusKm);
        _userRepoMock.Verify(r => r.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task UpdateProviderProfileAsync_ShouldReturnFalse_WhenUserIsClient()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Role = UserRole.Client }; // Not a provider
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var dto = new UpdateProviderProfileDto(20.0, "01001-000", 0, 0, new List<ServiceCategory>());

        // Act
        var result = await _service.UpdateProviderProfileAsync(userId, dto);

        // Assert
        Assert.False(result);
        _userRepoMock.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProviderOperationalStatusAsync_ShouldPersistStatus_WhenUserIsProvider()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Role = UserRole.Provider,
            ProviderProfile = new ProviderProfile()
        };
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var result = await _service.UpdateProviderOperationalStatusAsync(userId, ProviderOperationalStatus.EmAtendimento);

        Assert.True(result);
        Assert.Equal(ProviderOperationalStatus.EmAtendimento, user.ProviderProfile.OperationalStatus);
        _userRepoMock.Verify(r => r.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task GetProviderOperationalStatusAsync_ShouldReturnNull_WhenUserIsNotProvider()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Role = UserRole.Client
        };
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var result = await _service.GetProviderOperationalStatusAsync(userId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetProviderOperationalStatusAsync_ShouldReturnStatus_WhenUserIsProvider()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Role = UserRole.Provider,
            ProviderProfile = new ProviderProfile
            {
                OperationalStatus = ProviderOperationalStatus.Ausente
            }
        };
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var result = await _service.GetProviderOperationalStatusAsync(userId);

        Assert.Equal(ProviderOperationalStatus.Ausente, result);
    }
}
