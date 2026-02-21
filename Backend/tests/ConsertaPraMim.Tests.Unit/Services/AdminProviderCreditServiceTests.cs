using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminProviderCreditServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IProviderCreditService> _providerCreditServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<IAdminAuditLogRepository> _adminAuditLogRepositoryMock;
    private readonly AdminProviderCreditService _service;

    public AdminProviderCreditServiceTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _providerCreditServiceMock = new Mock<IProviderCreditService>();
        _notificationServiceMock = new Mock<INotificationService>();
        _adminAuditLogRepositoryMock = new Mock<IAdminAuditLogRepository>();

        _service = new AdminProviderCreditService(
            _userRepositoryMock.Object,
            _providerCreditServiceMock.Object,
            providerCreditRepository: null,
            _notificationServiceMock.Object,
            _adminAuditLogRepositoryMock.Object);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin prestador credito servico | Grant | Deve falhar quando campaign sem expiration.
    /// </summary>
    [Fact(DisplayName = "Admin prestador credito servico | Grant | Deve falhar quando campaign sem expiration")]
    public async Task GrantAsync_ShouldFail_WhenCampaignWithoutExpiration()
    {
        var providerId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(providerId))
            .ReturnsAsync(new User { Id = providerId, Role = UserRole.Provider, IsActive = true, Name = "Prestador", Email = "provider@teste.com" });

        var result = await _service.GrantAsync(
            new AdminProviderCreditGrantRequestDto(
                providerId,
                25m,
                "Campanha de engajamento",
                ProviderCreditGrantType.Campanha),
            Guid.NewGuid(),
            "admin@teste.com");

        Assert.False(result.Success);
        Assert.Equal("invalid_payload", result.ErrorCode);
        Assert.Contains("campanha", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        _providerCreditServiceMock.Verify(
            x => x.ApplyMutationAsync(It.IsAny<ProviderCreditMutationRequestDto>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin prestador credito servico | Grant | Deve apply mutation enviar notificacao e write audit.
    /// </summary>
    [Fact(DisplayName = "Admin prestador credito servico | Grant | Deve apply mutation enviar notificacao e write audit")]
    public async Task GrantAsync_ShouldApplyMutation_SendNotification_AndWriteAudit()
    {
        var providerId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(providerId))
            .ReturnsAsync(new User { Id = providerId, Role = UserRole.Provider, IsActive = true, Name = "Prestador", Email = "provider@teste.com" });

        _providerCreditServiceMock
            .Setup(x => x.GetBalanceAsync(providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderCreditBalanceDto(providerId, 10m, DateTime.UtcNow.AddMinutes(-20)));

        _providerCreditServiceMock
            .Setup(x => x.ApplyMutationAsync(It.IsAny<ProviderCreditMutationRequestDto>(), actorUserId, "admin@teste.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderCreditMutationResultDto(
                true,
                new ProviderCreditBalanceDto(providerId, 40m, DateTime.UtcNow),
                new ProviderCreditStatementItemDto(
                    Guid.NewGuid(),
                    ProviderCreditLedgerEntryType.Grant,
                    30m,
                    10m,
                    40m,
                    "Premio",
                    "AdminPortal.Premio",
                    "AdminCreditGrant",
                    null,
                    DateTime.UtcNow,
                    DateTime.UtcNow.AddDays(30),
                    actorUserId,
                    "admin@teste.com",
                    DateTime.UtcNow)));

        var result = await _service.GrantAsync(
            new AdminProviderCreditGrantRequestDto(
                providerId,
                30m,
                "Premio por qualidade no atendimento",
                ProviderCreditGrantType.Premio,
                DateTime.UtcNow.AddDays(30)),
            actorUserId,
            "admin@teste.com");

        Assert.True(result.Success);
        Assert.True(result.NotificationSent);
        Assert.NotNull(result.CreditMutation);

        _notificationServiceMock.Verify(
            x => x.SendNotificationAsync(
                providerId.ToString("N"),
                It.Is<string>(s => s.Contains("premio", StringComparison.OrdinalIgnoreCase) || s.Contains("credito", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<string>(),
                "/Profile"),
            Times.Once);

        _adminAuditLogRepositoryMock.Verify(
            x => x.AddAsync(It.Is<AdminAuditLog>(log =>
                log.ActorUserId == actorUserId &&
                log.Action == "AdminProviderCreditGrantExecuted" &&
                log.TargetId == providerId)),
            Times.Once);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin prestador credito servico | Reverse | Deve retornar failure quando ledger rejects insufficient balance.
    /// </summary>
    [Fact(DisplayName = "Admin prestador credito servico | Reverse | Deve retornar failure quando ledger rejects insufficient balance")]
    public async Task ReverseAsync_ShouldReturnFailure_WhenLedgerRejectsInsufficientBalance()
    {
        var providerId = Guid.NewGuid();
        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(providerId))
            .ReturnsAsync(new User { Id = providerId, Role = UserRole.Provider, IsActive = true, Name = "Prestador", Email = "provider@teste.com" });

        _providerCreditServiceMock
            .Setup(x => x.GetBalanceAsync(providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderCreditBalanceDto(providerId, 5m, DateTime.UtcNow.AddMinutes(-10)));

        _providerCreditServiceMock
            .Setup(x => x.ApplyMutationAsync(It.IsAny<ProviderCreditMutationRequestDto>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderCreditMutationResultDto(
                false,
                null,
                null,
                "insufficient_balance",
                "Saldo insuficiente para consumo/expiracao."));

        var result = await _service.ReverseAsync(
            new AdminProviderCreditReversalRequestDto(
                providerId,
                12m,
                "Estorno de premio lancado indevidamente"),
            Guid.NewGuid(),
            "admin@teste.com");

        Assert.False(result.Success);
        Assert.Equal("insufficient_balance", result.ErrorCode);
        _notificationServiceMock.Verify(
            x => x.SendNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()),
            Times.Never);
        _adminAuditLogRepositoryMock.Verify(
            x => x.AddAsync(It.IsAny<AdminAuditLog>()),
            Times.Never);
    }
}
