using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminDisputeQueueServiceTests
{
    private readonly Mock<IServiceDisputeCaseRepository> _disputeRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IAdminAuditLogRepository> _adminAuditRepositoryMock = new();
    private readonly Mock<IServicePaymentTransactionRepository> _paymentRepositoryMock = new();
    private readonly Mock<IPaymentService> _paymentServiceMock = new();
    private readonly Mock<IProviderCreditService> _providerCreditServiceMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();

    [Fact]
    public async Task UpdateWorkflowAsync_ShouldReturnForbidden_WhenActorIsNotAdmin()
    {
        var actorId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(actorId))
            .ReturnsAsync(new User
            {
                Id = actorId,
                Role = UserRole.Provider,
                Email = "provider@teste.com",
                Name = "Prestador"
            });

        var service = CreateService();

        var result = await service.UpdateWorkflowAsync(
            Guid.NewGuid(),
            actorId,
            "provider@teste.com",
            new AdminUpdateDisputeWorkflowRequestDto("UnderReview"));

        Assert.False(result.Success);
        Assert.Equal("forbidden", result.ErrorCode);
        _disputeRepositoryMock.Verify(r => r.GetByIdWithDetailsAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task RegisterDecisionAsync_ShouldReturnForbidden_WhenActorIsNotAdmin()
    {
        var actorId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(actorId))
            .ReturnsAsync(new User
            {
                Id = actorId,
                Role = UserRole.Client,
                Email = "cliente@teste.com",
                Name = "Cliente"
            });

        var service = CreateService();

        var result = await service.RegisterDecisionAsync(
            Guid.NewGuid(),
            actorId,
            "cliente@teste.com",
            new AdminRegisterDisputeDecisionRequestDto(
                Outcome: "procedente",
                Justification: "Teste de permissao"));

        Assert.False(result.Success);
        Assert.Equal("forbidden", result.ErrorCode);
        _disputeRepositoryMock.Verify(r => r.GetByIdWithDetailsAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task RegisterDecisionAsync_ShouldProceedPastPermissionGate_WhenActorIsAdmin()
    {
        var actorId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(actorId))
            .ReturnsAsync(new User
            {
                Id = actorId,
                Role = UserRole.Admin,
                Email = "admin@teste.com",
                Name = "Admin"
            });

        _disputeRepositoryMock
            .Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<Guid>()))
            .ReturnsAsync((ServiceDisputeCase?)null);

        var service = CreateService();

        var result = await service.RegisterDecisionAsync(
            Guid.NewGuid(),
            actorId,
            "admin@teste.com",
            new AdminRegisterDisputeDecisionRequestDto(
                Outcome: "procedente",
                Justification: "Admin autorizado"));

        Assert.False(result.Success);
        Assert.Equal("not_found", result.ErrorCode);
        _disputeRepositoryMock.Verify(r => r.GetByIdWithDetailsAsync(It.IsAny<Guid>()), Times.Once);
    }

    private AdminDisputeQueueService CreateService()
    {
        return new AdminDisputeQueueService(
            _disputeRepositoryMock.Object,
            _userRepositoryMock.Object,
            _adminAuditRepositoryMock.Object,
            _paymentRepositoryMock.Object,
            _paymentServiceMock.Object,
            _providerCreditServiceMock.Object,
            _notificationServiceMock.Object);
    }
}

