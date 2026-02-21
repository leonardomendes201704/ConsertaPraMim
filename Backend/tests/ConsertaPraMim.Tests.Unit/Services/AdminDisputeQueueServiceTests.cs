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

    [Fact(DisplayName = "Admin dispute queue servico | Atualizar workflow | Deve retornar proibido quando actor nao admin")]
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

    [Fact(DisplayName = "Admin dispute queue servico | Register decision | Deve retornar proibido quando actor nao admin")]
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

    [Fact(DisplayName = "Admin dispute queue servico | Register decision | Deve proceed past permission gate quando actor admin")]
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

    [Fact(DisplayName = "Admin dispute queue servico | Record case access | Deve append audit trail quando admin e case existe")]
    public async Task RecordCaseAccessAsync_ShouldAppendAuditTrail_WhenAdminAndCaseExists()
    {
        var actorId = Guid.NewGuid();
        var disputeId = Guid.NewGuid();
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
            .Setup(r => r.GetByIdAsync(disputeId))
            .ReturnsAsync(new ServiceDisputeCase
            {
                Id = disputeId,
                ServiceRequestId = Guid.NewGuid(),
                ServiceAppointmentId = Guid.NewGuid(),
                OpenedByUserId = Guid.NewGuid(),
                CounterpartyUserId = Guid.NewGuid(),
                ReasonCode = "OTHER",
                Description = "Teste",
                OpenedAtUtc = DateTime.UtcNow.AddHours(-2),
                SlaDueAtUtc = DateTime.UtcNow.AddHours(6),
                LastInteractionAtUtc = DateTime.UtcNow.AddHours(-1)
            });

        var service = CreateService();
        await service.RecordCaseAccessAsync(disputeId, actorId, "admin@teste.com", "unit_test");

        _disputeRepositoryMock.Verify(
            r => r.AddAuditEntryAsync(It.Is<ServiceDisputeCaseAuditEntry>(entry =>
                entry.ServiceDisputeCaseId == disputeId &&
                entry.ActorUserId == actorId &&
                entry.ActorRole == ServiceAppointmentActorRole.Admin &&
                entry.EventType == "dispute_case_viewed")),
            Times.Once);

        _adminAuditRepositoryMock.Verify(
            r => r.AddAsync(It.Is<AdminAuditLog>(log =>
                log.ActorUserId == actorId &&
                log.TargetId == disputeId &&
                log.Action == "DisputeCaseViewed")),
            Times.Once);
    }

    [Fact(DisplayName = "Admin dispute queue servico | Record case access | Deve ignore quando actor nao admin")]
    public async Task RecordCaseAccessAsync_ShouldIgnore_WhenActorIsNotAdmin()
    {
        var actorId = Guid.NewGuid();
        var disputeId = Guid.NewGuid();
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
        await service.RecordCaseAccessAsync(disputeId, actorId, "provider@teste.com", "unit_test");

        _disputeRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
        _disputeRepositoryMock.Verify(r => r.AddAuditEntryAsync(It.IsAny<ServiceDisputeCaseAuditEntry>()), Times.Never);
        _adminAuditRepositoryMock.Verify(r => r.AddAsync(It.IsAny<AdminAuditLog>()), Times.Never);
    }

    [Fact(DisplayName = "Admin dispute queue servico | Obter observability | Deve retornar anomaly alerts for frequency e recurrence")]
    public async Task GetObservabilityAsync_ShouldReturnAnomalyAlerts_ForFrequencyAndRecurrence()
    {
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var disputes = Enumerable.Range(1, 6)
            .Select(index => new ServiceDisputeCase
            {
                Id = Guid.NewGuid(),
                ServiceRequestId = Guid.NewGuid(),
                ServiceAppointmentId = Guid.NewGuid(),
                OpenedByUserId = userId,
                OpenedByRole = ServiceAppointmentActorRole.Client,
                OpenedByUser = new User
                {
                    Id = userId,
                    Name = "Cliente Suspeito",
                    Role = UserRole.Client
                },
                CounterpartyUserId = Guid.NewGuid(),
                CounterpartyRole = ServiceAppointmentActorRole.Provider,
                Type = DisputeCaseType.Billing,
                Priority = DisputeCasePriority.Medium,
                Status = index <= 4 ? DisputeCaseStatus.Rejected : DisputeCaseStatus.Open,
                ReasonCode = "PAYMENT_ISSUE",
                Description = "Teste",
                OpenedAtUtc = now.AddDays(-index),
                SlaDueAtUtc = now.AddDays(2),
                LastInteractionAtUtc = now.AddDays(-index).AddHours(2)
            })
            .ToList();

        _disputeRepositoryMock
            .Setup(r => r.GetCasesByOpenedPeriodAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>()))
            .ReturnsAsync(disputes);

        var service = CreateService();
        var dashboard = await service.GetObservabilityAsync(new AdminDisputeObservabilityQueryDto(
            now.AddDays(-30),
            now,
            10));

        Assert.NotNull(dashboard);
        Assert.True(dashboard.TotalDisputesOpened >= 6);
        Assert.NotEmpty(dashboard.AnomalyAlerts);
        Assert.Contains(dashboard.AnomalyAlerts, a => a.AlertCode == "HIGH_DISPUTE_FREQUENCY");
        Assert.Contains(dashboard.AnomalyAlerts, a => a.AlertCode == "REPEAT_REASON_PATTERN");
    }

    [Fact(DisplayName = "Admin dispute queue servico | Run retention | Deve retornar candidates on dry run")]
    public async Task RunRetentionAsync_ShouldReturnCandidates_OnDryRun()
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

        var oldClosedCase = new ServiceDisputeCase
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = Guid.NewGuid(),
            ServiceAppointmentId = Guid.NewGuid(),
            OpenedByUserId = Guid.NewGuid(),
            CounterpartyUserId = Guid.NewGuid(),
            Status = DisputeCaseStatus.Resolved,
            ReasonCode = "OTHER",
            Description = "Descricao original",
            OpenedAtUtc = DateTime.UtcNow.AddDays(-200),
            ClosedAtUtc = DateTime.UtcNow.AddDays(-190),
            SlaDueAtUtc = DateTime.UtcNow.AddDays(-180),
            LastInteractionAtUtc = DateTime.UtcNow.AddDays(-190)
        };

        _disputeRepositoryMock
            .Setup(r => r.GetClosedCasesClosedBeforeAsync(It.IsAny<DateTime>(), It.IsAny<int>()))
            .ReturnsAsync(new List<ServiceDisputeCase> { oldClosedCase });

        var service = CreateService();
        var result = await service.RunRetentionAsync(
            actorId,
            "admin@teste.com",
            new AdminDisputeRetentionRunRequestDto(
                RetentionDays: 180,
                Take: 100,
                DryRun: true));

        Assert.True(result.DryRun);
        Assert.Equal(1, result.Candidates);
        Assert.Equal(0, result.AnonymizedCases);
    }

    [Fact(DisplayName = "Admin dispute queue servico | Obter audit trail | Deve merge sources e apply normalized event filter")]
    public async Task GetAuditTrailAsync_ShouldMergeSources_AndApplyNormalizedEventFilter()
    {
        var actorId = Guid.NewGuid();
        var disputeId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        _disputeRepositoryMock
            .Setup(r => r.GetAuditEntriesByPeriodAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                actorId,
                disputeId,
                null,
                It.IsAny<int>()))
            .ReturnsAsync(new List<ServiceDisputeCaseAuditEntry>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ServiceDisputeCaseId = disputeId,
                    EventType = "dispute_case_viewed",
                    Message = "Visualizacao",
                    ActorUserId = actorId,
                    ActorRole = ServiceAppointmentActorRole.Admin,
                    CreatedAt = now.AddMinutes(-5),
                    ActorUser = new User
                    {
                        Id = actorId,
                        Name = "Admin Um",
                        Email = "admin1@teste.com"
                    }
                }
            });

        _adminAuditRepositoryMock
            .Setup(r => r.GetByTargetAndPeriodAsync(
                "ServiceDisputeCase",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                actorId,
                disputeId,
                null,
                It.IsAny<int>()))
            .ReturnsAsync(new List<AdminAuditLog>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ActorUserId = actorId,
                    ActorEmail = "admin1@teste.com",
                    Action = "DisputeDecisionRecorded",
                    TargetType = "ServiceDisputeCase",
                    TargetId = disputeId,
                    Metadata = "{}",
                    CreatedAt = now
                }
            });

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(actorId))
            .ReturnsAsync(new User
            {
                Id = actorId,
                Name = "Admin Um",
                Email = "admin1@teste.com",
                Role = UserRole.Admin
            });

        var service = CreateService();
        var response = await service.GetAuditTrailAsync(new AdminDisputeAuditQueryDto(
            FromUtc: now.AddDays(-1),
            ToUtc: now.AddDays(1),
            ActorUserId: actorId,
            DisputeCaseId: disputeId,
            EventType: "dispute decision-recorded",
            Take: 20));

        Assert.NotNull(response);
        Assert.Single(response.Items);
        Assert.Equal("AdminAudit", response.Items[0].Source);
        Assert.Equal("DisputeDecisionRecorded", response.Items[0].EventType);

        _disputeRepositoryMock.Verify(
            r => r.GetAuditEntriesByPeriodAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                actorId,
                disputeId,
                null,
                It.IsAny<int>()),
            Times.Once);
        _adminAuditRepositoryMock.Verify(
            r => r.GetByTargetAndPeriodAsync(
                "ServiceDisputeCase",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                actorId,
                disputeId,
                null,
                It.IsAny<int>()),
            Times.Once);
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
