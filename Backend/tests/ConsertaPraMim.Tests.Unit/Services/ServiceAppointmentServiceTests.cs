using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class ServiceAppointmentServiceTests
{
    private readonly Mock<IServiceAppointmentRepository> _appointmentRepositoryMock;
    private readonly Mock<IServiceRequestRepository> _requestRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<IAppointmentReminderService> _appointmentReminderServiceMock;
    private readonly Mock<IServiceAppointmentChecklistService> _checklistServiceMock;
    private readonly Mock<IServiceCompletionTermRepository> _completionTermRepositoryMock;
    private readonly Mock<IServiceScopeChangeRequestRepository> _scopeChangeRequestRepositoryMock;
    private readonly Mock<IServiceWarrantyClaimRepository> _serviceWarrantyClaimRepositoryMock;
    private readonly Mock<IServiceRequestCommercialValueService> _commercialValueServiceMock;
    private readonly Mock<IServiceFinancialPolicyCalculationService> _financialPolicyCalculationServiceMock;
    private readonly Mock<IProviderCreditService> _providerCreditServiceMock;
    private readonly Mock<IAdminAuditLogRepository> _adminAuditLogRepositoryMock;
    private readonly ServiceAppointmentService _service;

    public ServiceAppointmentServiceTests()
    {
        _appointmentRepositoryMock = new Mock<IServiceAppointmentRepository>();
        _requestRepositoryMock = new Mock<IServiceRequestRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _notificationServiceMock = new Mock<INotificationService>();
        _appointmentReminderServiceMock = new Mock<IAppointmentReminderService>();
        _checklistServiceMock = new Mock<IServiceAppointmentChecklistService>();
        _completionTermRepositoryMock = new Mock<IServiceCompletionTermRepository>();
        _scopeChangeRequestRepositoryMock = new Mock<IServiceScopeChangeRequestRepository>();
        _serviceWarrantyClaimRepositoryMock = new Mock<IServiceWarrantyClaimRepository>();
        _commercialValueServiceMock = new Mock<IServiceRequestCommercialValueService>();
        _financialPolicyCalculationServiceMock = new Mock<IServiceFinancialPolicyCalculationService>();
        _providerCreditServiceMock = new Mock<IProviderCreditService>();
        _adminAuditLogRepositoryMock = new Mock<IAdminAuditLogRepository>();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceAppointments:ConfirmationExpiryHours"] = "12",
                ["ServiceAppointments:CancelMinimumHoursBeforeWindow"] = "2",
                ["ServiceAppointments:RescheduleMinimumHoursBeforeWindow"] = "2",
                ["ServiceAppointments:RescheduleMaximumAdvanceDays"] = "30",
                ["ServiceAppointments:AvailabilityTimeZoneId"] = "UTC",
                ["ServiceAppointments:CompletionPinLength"] = "6",
                ["ServiceAppointments:CompletionPinExpiryMinutes"] = "30",
                ["ServiceAppointments:CompletionPinMaxFailedAttempts"] = "5"
            })
            .Build();

        _completionTermRepositoryMock
            .Setup(r => r.GetByAppointmentIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((ServiceCompletionTerm?)null);

        _userRepositoryMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(Array.Empty<User>());

        _scopeChangeRequestRepositoryMock
            .Setup(r => r.GetLatestByAppointmentIdAndStatusAsync(
                It.IsAny<Guid>(),
                It.IsAny<ServiceScopeChangeRequestStatus>()))
            .ReturnsAsync((ServiceScopeChangeRequest?)null);

        _scopeChangeRequestRepositoryMock
            .Setup(r => r.GetByServiceRequestIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<ServiceScopeChangeRequest>());

        _scopeChangeRequestRepositoryMock
            .Setup(r => r.GetExpiredPendingByRequestedAtAsync(It.IsAny<DateTime>(), It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<ServiceScopeChangeRequest>());

        _serviceWarrantyClaimRepositoryMock
            .Setup(r => r.GetByAppointmentIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<ServiceWarrantyClaim>());

        _appointmentReminderServiceMock
            .Setup(r => r.RegisterPresenceResponseTelemetryAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        _commercialValueServiceMock
            .Setup(s => s.RecalculateAsync(It.IsAny<ServiceRequest>()))
            .ReturnsAsync((ServiceRequest request) =>
            {
                var acceptedProposalValue = (request.Proposals ?? Array.Empty<Proposal>())
                    .Where(p => p.Accepted && !p.IsInvalidated)
                    .Select(p => p.EstimatedValue)
                    .Where(v => v.HasValue && v.Value > 0m)
                    .Select(v => v!.Value)
                    .DefaultIfEmpty(0m)
                    .Max();

                var baseValue = request.CommercialBaseValue ?? acceptedProposalValue;
                var currentValue = request.CommercialCurrentValue ?? baseValue;
                var approvedIncremental = Math.Max(0m, currentValue - baseValue);

                return new ServiceRequestCommercialTotalsDto(baseValue, approvedIncremental, currentValue);
            });

        _financialPolicyCalculationServiceMock
            .Setup(s => s.CalculateAsync(It.IsAny<ServiceFinancialCalculationRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceFinancialCalculationResultDto(
                false,
                ErrorCode: "policy_rule_not_found",
                ErrorMessage: "Regra nao encontrada."));

        _providerCreditServiceMock
            .Setup(s => s.ApplyMutationAsync(
                It.IsAny<ProviderCreditMutationRequestDto>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderCreditMutationResultDto(
                false,
                ErrorCode: "mutation_not_applied",
                ErrorMessage: "Sem mutacao."));

        _service = new ServiceAppointmentService(
            _appointmentRepositoryMock.Object,
            _requestRepositoryMock.Object,
            _userRepositoryMock.Object,
            _notificationServiceMock.Object,
            configuration,
            _appointmentReminderServiceMock.Object,
            _checklistServiceMock.Object,
            _completionTermRepositoryMock.Object,
            _scopeChangeRequestRepositoryMock.Object,
            _commercialValueServiceMock.Object,
            _financialPolicyCalculationServiceMock.Object,
            _providerCreditServiceMock.Object,
            _adminAuditLogRepositoryMock.Object,
            _serviceWarrantyClaimRepositoryMock.Object);
    }

    [Fact]
    public async Task GetAvailableSlotsAsync_ShouldReturnForbidden_WhenProviderQueriesAnotherProvider()
    {
        var actorProviderId = Guid.NewGuid();
        var otherProviderId = Guid.NewGuid();
        var query = new GetServiceAppointmentSlotsQueryDto(
            otherProviderId,
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(6));

        var result = await _service.GetAvailableSlotsAsync(actorProviderId, UserRole.Provider.ToString(), query);

        Assert.False(result.Success);
        Assert.Equal("forbidden", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnProviderNotAssigned_WhenProposalIsNotAccepted()
    {
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var request = BuildRequest(clientId, providerId, acceptedProposal: false);
        request.Id = requestId;

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(providerId))
            .ReturnsAsync(new User { Id = providerId, Role = UserRole.Provider, IsActive = true });

        _requestRepositoryMock
            .Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(request);

        var windowStartUtc = NextUtcDayOfWeek(DateTime.UtcNow, DayOfWeek.Monday).AddHours(10);
        var windowEndUtc = windowStartUtc.AddHours(1);
        var dto = new CreateServiceAppointmentRequestDto(
            requestId,
            providerId,
            windowStartUtc,
            windowEndUtc);

        var result = await _service.CreateAsync(clientId, UserRole.Client.ToString(), dto);

        Assert.False(result.Success);
        Assert.Equal("provider_not_assigned", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateAppointment_WhenRequestAndSlotAreValid()
    {
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var windowStartUtc = NextUtcDayOfWeek(DateTime.UtcNow, DayOfWeek.Monday).AddHours(10);
        var windowEndUtc = windowStartUtc.AddHours(1);
        var request = BuildRequest(clientId, providerId, acceptedProposal: true);
        request.Id = requestId;
        request.Status = ServiceRequestStatus.Created;

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(providerId))
            .ReturnsAsync(new User { Id = providerId, Role = UserRole.Provider, IsActive = true });

        _requestRepositoryMock
            .Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(request);

        _appointmentRepositoryMock
            .SetupSequence(r => r.GetByRequestIdAsync(requestId))
            .ReturnsAsync(Array.Empty<ServiceAppointment>())
            .ReturnsAsync(new List<ServiceAppointment>
            {
                new()
                {
                    ServiceRequestId = requestId,
                    Status = ServiceAppointmentStatus.PendingProviderConfirmation,
                    WindowStartUtc = windowStartUtc,
                    WindowEndUtc = windowEndUtc
                }
            });

        _appointmentRepositoryMock
            .Setup(r => r.GetAvailabilityRulesByProviderAsync(providerId))
            .ReturnsAsync(new List<ProviderAvailabilityRule>
            {
                new()
                {
                    ProviderId = providerId,
                    DayOfWeek = DayOfWeek.Monday,
                    StartTime = TimeSpan.FromHours(8),
                    EndTime = TimeSpan.FromHours(18),
                    SlotDurationMinutes = 30,
                    IsActive = true
                }
            });

        _appointmentRepositoryMock
            .Setup(r => r.GetAvailabilityExceptionsByProviderAsync(
                providerId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync(Array.Empty<ProviderAvailabilityException>());

        _appointmentRepositoryMock
            .Setup(r => r.GetProviderAppointmentsByStatusesInRangeAsync(
                providerId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<IReadOnlyCollection<ServiceAppointmentStatus>>()))
            .ReturnsAsync(Array.Empty<ServiceAppointment>());

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((ServiceAppointment?)null);

        var dto = new CreateServiceAppointmentRequestDto(
            requestId,
            providerId,
            windowStartUtc,
            windowEndUtc,
            "Agendamento inicial");

        var result = await _service.CreateAsync(clientId, UserRole.Client.ToString(), dto);

        Assert.True(result.Success, $"{result.ErrorCode} - {result.ErrorMessage}");
        Assert.NotNull(result.Appointment);
        Assert.Equal(requestId, result.Appointment!.ServiceRequestId);
        Assert.Equal(ServiceRequestStatus.Scheduled, request.Status);

        _appointmentRepositoryMock.Verify(r => r.AddAsync(It.IsAny<ServiceAppointment>()), Times.Once);
        _appointmentRepositoryMock.Verify(r => r.AddHistoryAsync(It.IsAny<ServiceAppointmentHistory>()), Times.Once);
        _requestRepositoryMock.Verify(r => r.UpdateAsync(request), Times.Once);
    }

    [Fact]
    public async Task ConfirmAsync_ShouldReturnInvalidState_WhenAppointmentIsNotPending()
    {
        var providerId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ProviderId = providerId,
                ClientId = Guid.NewGuid(),
                ServiceRequestId = Guid.NewGuid(),
                Status = ServiceAppointmentStatus.RejectedByProvider
            });

        var result = await _service.ConfirmAsync(providerId, UserRole.Provider.ToString(), appointmentId);

        Assert.False(result.Success);
        Assert.Equal("invalid_state", result.ErrorCode);
    }

    [Fact]
    public async Task ConfirmAsync_ShouldConfirmAppointment_WhenPending()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var request = BuildRequest(clientId, providerId, acceptedProposal: true);
        request.Id = requestId;
        request.Status = ServiceRequestStatus.Scheduled;

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ProviderId = providerId,
                ClientId = clientId,
                ServiceRequestId = requestId,
                Status = ServiceAppointmentStatus.PendingProviderConfirmation,
                ServiceRequest = request
            });

        var result = await _service.ConfirmAsync(providerId, UserRole.Provider.ToString(), appointmentId);

        Assert.True(result.Success, $"{result.ErrorCode} - {result.ErrorMessage}");
        _appointmentRepositoryMock.Verify(r => r.UpdateAsync(It.Is<ServiceAppointment>(a =>
            a.Status == ServiceAppointmentStatus.Confirmed &&
            a.ConfirmedAtUtc.HasValue)), Times.Once);
        _appointmentRepositoryMock.Verify(r => r.AddHistoryAsync(It.IsAny<ServiceAppointmentHistory>()), Times.Once);
    }

    [Fact]
    public async Task RespondPresenceAsync_ShouldRegisterClientConfirmation()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        var appointment = new ServiceAppointment
        {
            Id = appointmentId,
            ProviderId = providerId,
            ClientId = clientId,
            ServiceRequestId = requestId,
            Status = ServiceAppointmentStatus.Confirmed
        };

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(appointment);

        var result = await _service.RespondPresenceAsync(
            clientId,
            UserRole.Client.ToString(),
            appointmentId,
            new RespondServiceAppointmentPresenceRequestDto(true, "Estarei no local."));

        Assert.True(result.Success, $"{result.ErrorCode} - {result.ErrorMessage}");
        Assert.NotNull(result.Appointment);
        Assert.True(result.Appointment!.ClientPresenceConfirmed);
        Assert.NotNull(result.Appointment.ClientPresenceRespondedAtUtc);
        Assert.Equal("Estarei no local.", result.Appointment.ClientPresenceReason);

        _appointmentRepositoryMock.Verify(r => r.UpdateAsync(It.Is<ServiceAppointment>(a =>
            a.Id == appointmentId &&
            a.ClientPresenceConfirmed == true &&
            a.ClientPresenceRespondedAtUtc.HasValue &&
            a.ClientPresenceReason == "Estarei no local.")), Times.Once);
        _appointmentRepositoryMock.Verify(r => r.AddHistoryAsync(It.Is<ServiceAppointmentHistory>(h =>
            h.ServiceAppointmentId == appointmentId &&
            h.ActorUserId == clientId &&
            h.Metadata != null &&
            h.Metadata.Contains("\"participant\":\"client\"") &&
            h.Metadata.Contains("\"confirmed\":true"))), Times.Once);
        _notificationServiceMock.Verify(n => n.SendNotificationAsync(
                providerId.ToString("N"),
                "Agendamento: resposta de presenca",
                It.Is<string>(m => m.Contains("Cliente", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<string>()),
            Times.Once);
        _appointmentReminderServiceMock.Verify(r => r.RegisterPresenceResponseTelemetryAsync(
                appointmentId,
                clientId,
                true,
                "Estarei no local.",
                It.IsAny<DateTime>()),
            Times.Once);
    }

    [Fact]
    public async Task RespondPresenceAsync_ShouldReturnInvalidState_WhenAppointmentIsCompleted()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ProviderId = providerId,
                ClientId = clientId,
                ServiceRequestId = Guid.NewGuid(),
                Status = ServiceAppointmentStatus.Completed
            });

        var result = await _service.RespondPresenceAsync(
            providerId,
            UserRole.Provider.ToString(),
            appointmentId,
            new RespondServiceAppointmentPresenceRequestDto(false, "Imprevisto"));

        Assert.False(result.Success);
        Assert.Equal("invalid_state", result.ErrorCode);
        _appointmentRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<ServiceAppointment>()), Times.Never);
        _appointmentReminderServiceMock.Verify(r => r.RegisterPresenceResponseTelemetryAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<bool>(),
            It.IsAny<string?>(),
            It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task RejectAsync_ShouldRejectAndReturnSuccess_WhenPendingAndReasonProvided()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var request = BuildRequest(clientId, providerId, acceptedProposal: true);
        request.Id = requestId;
        request.Status = ServiceRequestStatus.Scheduled;

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ProviderId = providerId,
                ClientId = clientId,
                ServiceRequestId = requestId,
                Status = ServiceAppointmentStatus.PendingProviderConfirmation,
                ServiceRequest = request
            });

        var result = await _service.RejectAsync(
            providerId,
            UserRole.Provider.ToString(),
            appointmentId,
            new RejectServiceAppointmentRequestDto("Nao tenho disponibilidade"));

        Assert.True(result.Success);
        _appointmentRepositoryMock.Verify(r => r.UpdateAsync(It.Is<ServiceAppointment>(a =>
            a.Status == ServiceAppointmentStatus.RejectedByProvider &&
            a.Reason == "Nao tenho disponibilidade")), Times.Once);
        _appointmentRepositoryMock.Verify(r => r.AddHistoryAsync(It.IsAny<ServiceAppointmentHistory>()), Times.Once);
        _requestRepositoryMock.Verify(r => r.UpdateAsync(request), Times.Once);
    }

    [Fact]
    public async Task RequestRescheduleAsync_ShouldCreatePendingRequest_WhenConfirmedAndWindowIsAvailable()
    {
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var currentWindowStartUtc = NextUtcDayOfWeek(DateTime.UtcNow, DayOfWeek.Monday).AddHours(10);
        var currentWindowEndUtc = currentWindowStartUtc.AddHours(1);
        var proposedWindowStartUtc = currentWindowStartUtc.AddHours(2);
        var proposedWindowEndUtc = proposedWindowStartUtc.AddHours(1);

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ServiceRequestId = requestId,
                ClientId = clientId,
                ProviderId = providerId,
                Status = ServiceAppointmentStatus.Confirmed,
                WindowStartUtc = currentWindowStartUtc,
                WindowEndUtc = currentWindowEndUtc,
                ServiceRequest = BuildRequest(clientId, providerId, acceptedProposal: true)
            });

        _appointmentRepositoryMock
            .Setup(r => r.GetAvailabilityRulesByProviderAsync(providerId))
            .ReturnsAsync(new List<ProviderAvailabilityRule>
            {
                new()
                {
                    ProviderId = providerId,
                    DayOfWeek = proposedWindowStartUtc.DayOfWeek,
                    StartTime = TimeSpan.FromHours(8),
                    EndTime = TimeSpan.FromHours(22),
                    SlotDurationMinutes = 30,
                    IsActive = true
                }
            });

        _appointmentRepositoryMock
            .Setup(r => r.GetAvailabilityExceptionsByProviderAsync(
                providerId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync(Array.Empty<ProviderAvailabilityException>());

        _appointmentRepositoryMock
            .Setup(r => r.GetProviderAppointmentsByStatusesInRangeAsync(
                providerId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<IReadOnlyCollection<ServiceAppointmentStatus>>()))
            .ReturnsAsync(Array.Empty<ServiceAppointment>());

        var result = await _service.RequestRescheduleAsync(
            clientId,
            UserRole.Client.ToString(),
            appointmentId,
            new RequestServiceAppointmentRescheduleDto(
                proposedWindowStartUtc,
                proposedWindowEndUtc,
                "Preciso de outro horario"));

        Assert.True(result.Success);
        _appointmentRepositoryMock.Verify(r => r.UpdateAsync(It.Is<ServiceAppointment>(a =>
            a.Status == ServiceAppointmentStatus.RescheduleRequestedByClient &&
            a.ProposedWindowStartUtc == proposedWindowStartUtc &&
            a.ProposedWindowEndUtc == proposedWindowEndUtc &&
            a.RescheduleRequestReason == "Preciso de outro horario")), Times.Once);
        _appointmentRepositoryMock.Verify(r => r.AddHistoryAsync(It.IsAny<ServiceAppointmentHistory>()), Times.Once);
    }

    [Fact]
    public async Task RespondRescheduleAsync_ShouldAcceptAndApplyNewWindow_WhenCounterpartyAccepts()
    {
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var currentWindowStartUtc = NextUtcDayOfWeek(DateTime.UtcNow, DayOfWeek.Monday).AddHours(10);
        var currentWindowEndUtc = currentWindowStartUtc.AddHours(1);
        var proposedWindowStartUtc = currentWindowStartUtc.AddHours(3);
        var proposedWindowEndUtc = proposedWindowStartUtc.AddHours(1);

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ServiceRequestId = requestId,
                ClientId = clientId,
                ProviderId = providerId,
                Status = ServiceAppointmentStatus.RescheduleRequestedByClient,
                WindowStartUtc = currentWindowStartUtc,
                WindowEndUtc = currentWindowEndUtc,
                ProposedWindowStartUtc = proposedWindowStartUtc,
                ProposedWindowEndUtc = proposedWindowEndUtc,
                RescheduleRequestReason = "Troca de compromisso",
                ServiceRequest = BuildRequest(clientId, providerId, acceptedProposal: true)
            });

        _appointmentRepositoryMock
            .Setup(r => r.GetAvailabilityRulesByProviderAsync(providerId))
            .ReturnsAsync(new List<ProviderAvailabilityRule>
            {
                new()
                {
                    ProviderId = providerId,
                    DayOfWeek = proposedWindowStartUtc.DayOfWeek,
                    StartTime = TimeSpan.FromHours(8),
                    EndTime = TimeSpan.FromHours(22),
                    SlotDurationMinutes = 30,
                    IsActive = true
                }
            });

        _appointmentRepositoryMock
            .Setup(r => r.GetAvailabilityExceptionsByProviderAsync(
                providerId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync(Array.Empty<ProviderAvailabilityException>());

        _appointmentRepositoryMock
            .Setup(r => r.GetProviderAppointmentsByStatusesInRangeAsync(
                providerId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<IReadOnlyCollection<ServiceAppointmentStatus>>()))
            .ReturnsAsync(Array.Empty<ServiceAppointment>());

        var result = await _service.RespondRescheduleAsync(
            providerId,
            UserRole.Provider.ToString(),
            appointmentId,
            new RespondServiceAppointmentRescheduleRequestDto(true));

        Assert.True(result.Success);
        _appointmentRepositoryMock.Verify(r => r.UpdateAsync(It.Is<ServiceAppointment>(a =>
            a.Status == ServiceAppointmentStatus.RescheduleConfirmed &&
            a.WindowStartUtc == proposedWindowStartUtc &&
            a.WindowEndUtc == proposedWindowEndUtc &&
            a.ProposedWindowStartUtc == null &&
            a.ProposedWindowEndUtc == null)), Times.Once);
        _appointmentRepositoryMock.Verify(r => r.AddHistoryAsync(It.IsAny<ServiceAppointmentHistory>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_ShouldCancelAndKeepRequestSchedulable_WhenClientCancelsWithAntecedence()
    {
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var request = BuildRequest(clientId, providerId, acceptedProposal: true);
        request.Id = requestId;
        request.Status = ServiceRequestStatus.Scheduled;

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ServiceRequestId = requestId,
                ClientId = clientId,
                ProviderId = providerId,
                Status = ServiceAppointmentStatus.Confirmed,
                WindowStartUtc = DateTime.UtcNow.AddHours(8),
                WindowEndUtc = DateTime.UtcNow.AddHours(9),
                ServiceRequest = request
            });

        var result = await _service.CancelAsync(
            clientId,
            UserRole.Client.ToString(),
            appointmentId,
            new CancelServiceAppointmentRequestDto("Nao estarei em casa"));

        Assert.True(result.Success);
        _appointmentRepositoryMock.Verify(r => r.UpdateAsync(It.Is<ServiceAppointment>(a =>
            a.Status == ServiceAppointmentStatus.CancelledByClient &&
            a.CancelledAtUtc.HasValue &&
            a.Reason == "Nao estarei em casa")), Times.Once);
        _requestRepositoryMock.Verify(r => r.UpdateAsync(It.Is<ServiceRequest>(sr =>
            sr.Id == requestId &&
            sr.Status == ServiceRequestStatus.Matching)), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_ShouldApplyFinancialCompensationGrantToProvider_WhenClientCancels()
    {
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var nowUtc = DateTime.UtcNow;

        var request = BuildRequest(clientId, providerId, acceptedProposal: true);
        request.Id = requestId;
        request.Status = ServiceRequestStatus.Scheduled;
        request.CommercialBaseValue = 200m;
        request.CommercialCurrentValue = 200m;

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ServiceRequestId = requestId,
                ClientId = clientId,
                ProviderId = providerId,
                Status = ServiceAppointmentStatus.Confirmed,
                WindowStartUtc = nowUtc.AddHours(8),
                WindowEndUtc = nowUtc.AddHours(9),
                ServiceRequest = request
            });

        _financialPolicyCalculationServiceMock
            .Setup(s => s.CalculateAsync(
                It.Is<ServiceFinancialCalculationRequestDto>(q =>
                    q.EventType == ServiceFinancialPolicyEventType.ClientCancellation &&
                    q.ServiceValue == 200m),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceFinancialCalculationResultDto(
                true,
                new ServiceFinancialCalculationBreakdownDto(
                    Guid.NewGuid(),
                    "Cancelamento cliente 4h-24h",
                    ServiceFinancialPolicyEventType.ClientCancellation,
                    200m,
                    8d,
                    4,
                    24,
                    1,
                    20m,
                    40m,
                    15m,
                    30m,
                    5m,
                    10m,
                    160m,
                    "Provider",
                    "memo")));

        _providerCreditServiceMock
            .Setup(s => s.ApplyMutationAsync(
                It.IsAny<ProviderCreditMutationRequestDto>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderCreditMutationResultDto(true));

        var result = await _service.CancelAsync(
            clientId,
            UserRole.Client.ToString(),
            appointmentId,
            new CancelServiceAppointmentRequestDto("Nao estarei em casa"));

        Assert.True(result.Success);
        _providerCreditServiceMock.Verify(s => s.ApplyMutationAsync(
            It.Is<ProviderCreditMutationRequestDto>(r =>
                r.ProviderId == providerId &&
                r.EntryType == ProviderCreditLedgerEntryType.Grant &&
                r.Amount == 30m &&
                r.ReferenceType == "ServiceAppointment" &&
                r.ReferenceId == appointmentId &&
                r.Source != null &&
                r.Source.Contains("FinancialPolicy:cancel_Client", StringComparison.Ordinal)),
            clientId,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
        _adminAuditLogRepositoryMock.Verify(r => r.AddAsync(It.Is<AdminAuditLog>(a =>
            a.Action == "ServiceFinancialPolicyEventGenerated" &&
            a.TargetType == "ServiceAppointmentFinancialPolicy" &&
            a.TargetId == appointmentId &&
            a.Metadata != null &&
            a.Metadata.Contains("\"outcome\":\"ledger_applied\"") &&
            a.Metadata.Contains("\"eventType\":\"ClientCancellation\""))), Times.Once);
    }

    [Fact]
    public async Task OverrideFinancialPolicyAsync_ShouldReturnInvalidJustification_WhenReasonIsMissing()
    {
        var result = await _service.OverrideFinancialPolicyAsync(
            Guid.NewGuid(),
            UserRole.Admin.ToString(),
            Guid.NewGuid(),
            new ServiceFinancialPolicyOverrideRequestDto(
                ServiceFinancialPolicyEventType.ClientCancellation,
                "  ",
                DateTime.UtcNow));

        Assert.False(result.Success);
        Assert.Equal("invalid_justification", result.ErrorCode);
        _appointmentRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task OverrideFinancialPolicyAsync_ShouldAppendAuditTrailAndApplyMutation_WhenAdminOverrides()
    {
        var adminId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var eventOccurredAtUtc = DateTime.UtcNow;
        var justification = "Suporte validou excecao por indisponibilidade comprovada.";

        var request = BuildRequest(clientId, providerId, acceptedProposal: true);
        request.Id = requestId;
        request.Status = ServiceRequestStatus.Scheduled;
        request.CommercialBaseValue = 250m;
        request.CommercialCurrentValue = 250m;

        var appointment = new ServiceAppointment
        {
            Id = appointmentId,
            ServiceRequestId = requestId,
            ClientId = clientId,
            ProviderId = providerId,
            Status = ServiceAppointmentStatus.Confirmed,
            WindowStartUtc = eventOccurredAtUtc.AddHours(10),
            WindowEndUtc = eventOccurredAtUtc.AddHours(11),
            ServiceRequest = request
        };

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(appointment);

        _financialPolicyCalculationServiceMock
            .Setup(s => s.CalculateAsync(
                It.Is<ServiceFinancialCalculationRequestDto>(q =>
                    q.EventType == ServiceFinancialPolicyEventType.ClientCancellation &&
                    q.ServiceValue == 250m),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceFinancialCalculationResultDto(
                true,
                new ServiceFinancialCalculationBreakdownDto(
                    Guid.NewGuid(),
                    "Override - cancelamento cliente",
                    ServiceFinancialPolicyEventType.ClientCancellation,
                    250m,
                    12d,
                    4,
                    24,
                    1,
                    20m,
                    50m,
                    15m,
                    37.5m,
                    5m,
                    12.5m,
                    200m,
                    "Provider",
                    "memo")));

        _providerCreditServiceMock
            .Setup(s => s.ApplyMutationAsync(
                It.IsAny<ProviderCreditMutationRequestDto>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderCreditMutationResultDto(true));

        var result = await _service.OverrideFinancialPolicyAsync(
            adminId,
            UserRole.Admin.ToString(),
            appointmentId,
            new ServiceFinancialPolicyOverrideRequestDto(
                ServiceFinancialPolicyEventType.ClientCancellation,
                justification,
                eventOccurredAtUtc));

        Assert.True(result.Success, $"{result.ErrorCode} - {result.ErrorMessage}");
        _appointmentRepositoryMock.Verify(r => r.AddHistoryAsync(It.Is<ServiceAppointmentHistory>(h =>
            h.ServiceAppointmentId == appointmentId &&
            h.ActorUserId == adminId &&
            h.ActorRole == ServiceAppointmentActorRole.Admin &&
            h.Metadata != null &&
            h.Metadata.Contains("financial_policy_override_requested") &&
            h.Metadata.Contains(justification))), Times.Once);
        _appointmentRepositoryMock.Verify(r => r.AddHistoryAsync(It.Is<ServiceAppointmentHistory>(h =>
            h.ServiceAppointmentId == appointmentId &&
            h.Metadata != null &&
            h.Metadata.Contains("financial_policy_application"))), Times.Once);
        _providerCreditServiceMock.Verify(s => s.ApplyMutationAsync(
            It.Is<ProviderCreditMutationRequestDto>(m =>
                m.ProviderId == providerId &&
                m.EntryType == ProviderCreditLedgerEntryType.Grant &&
                m.Amount == 37.5m &&
                m.Source != null &&
                m.Source.Contains("FinancialPolicy:admin_override_ClientCancellation", StringComparison.Ordinal)),
            adminId,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
        _adminAuditLogRepositoryMock.Verify(r => r.AddAsync(It.Is<AdminAuditLog>(a =>
            a.Action == "ServiceFinancialPolicyEventGenerated" &&
            a.TargetType == "ServiceAppointmentFinancialPolicy" &&
            a.TargetId == appointmentId &&
            a.Metadata != null &&
            a.Metadata.Contains("\"eventType\":\"ClientCancellation\"") &&
            a.Metadata.Contains("\"outcome\":\"ledger_applied\""))), Times.Once);
        _notificationServiceMock.Verify(n => n.SendNotificationAsync(
            clientId.ToString("N"),
            "Ajuste financeiro administrativo",
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
        _notificationServiceMock.Verify(n => n.SendNotificationAsync(
            providerId.ToString("N"),
            "Ajuste financeiro administrativo",
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ExpirePendingAppointmentsAsync_ShouldExpireOverduePendingAppointments()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var request = BuildRequest(clientId, providerId, acceptedProposal: true);
        request.Id = Guid.NewGuid();
        request.Status = ServiceRequestStatus.Scheduled;

        _appointmentRepositoryMock
            .Setup(r => r.GetExpiredPendingAppointmentsAsync(It.IsAny<DateTime>(), It.IsAny<int>()))
            .ReturnsAsync(new List<ServiceAppointment>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ProviderId = providerId,
                    ClientId = clientId,
                    ServiceRequestId = request.Id,
                    Status = ServiceAppointmentStatus.PendingProviderConfirmation,
                    ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5),
                    ServiceRequest = request
                }
            });

        var result = await _service.ExpirePendingAppointmentsAsync();

        Assert.Equal(1, result);
        _appointmentRepositoryMock.Verify(r => r.UpdateAsync(It.Is<ServiceAppointment>(a =>
            a.Status == ServiceAppointmentStatus.ExpiredWithoutProviderAction)), Times.Once);
        _appointmentRepositoryMock.Verify(r => r.AddHistoryAsync(It.IsAny<ServiceAppointmentHistory>()), Times.Once);
        _requestRepositoryMock.Verify(r => r.UpdateAsync(request), Times.Once);
    }

    [Fact]
    public async Task ExpirePendingAppointmentsAsync_ShouldApplyFinancialPenaltyDebitToProvider_WhenNoShowOccurs()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var request = BuildRequest(clientId, providerId, acceptedProposal: true);
        request.Id = Guid.NewGuid();
        request.Status = ServiceRequestStatus.Scheduled;
        request.CommercialBaseValue = 300m;
        request.CommercialCurrentValue = 300m;

        var appointmentId = Guid.NewGuid();
        _appointmentRepositoryMock
            .Setup(r => r.GetExpiredPendingAppointmentsAsync(It.IsAny<DateTime>(), It.IsAny<int>()))
            .ReturnsAsync(new List<ServiceAppointment>
            {
                new()
                {
                    Id = appointmentId,
                    ProviderId = providerId,
                    ClientId = clientId,
                    ServiceRequestId = request.Id,
                    Status = ServiceAppointmentStatus.PendingProviderConfirmation,
                    ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5),
                    WindowStartUtc = DateTime.UtcNow.AddHours(6),
                    WindowEndUtc = DateTime.UtcNow.AddHours(7),
                    ServiceRequest = request
                }
            });

        _financialPolicyCalculationServiceMock
            .Setup(s => s.CalculateAsync(
                It.Is<ServiceFinancialCalculationRequestDto>(q =>
                    q.EventType == ServiceFinancialPolicyEventType.ProviderNoShow &&
                    q.ServiceValue == 300m),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceFinancialCalculationResultDto(
                true,
                new ServiceFinancialCalculationBreakdownDto(
                    Guid.NewGuid(),
                    "No-show prestador",
                    ServiceFinancialPolicyEventType.ProviderNoShow,
                    300m,
                    0d,
                    0,
                    null,
                    1,
                    40m,
                    120m,
                    30m,
                    90m,
                    10m,
                    30m,
                    180m,
                    "Client",
                    "memo")));

        _providerCreditServiceMock
            .Setup(s => s.ApplyMutationAsync(
                It.IsAny<ProviderCreditMutationRequestDto>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderCreditMutationResultDto(true));

        var result = await _service.ExpirePendingAppointmentsAsync();

        Assert.Equal(1, result);
        _providerCreditServiceMock.Verify(s => s.ApplyMutationAsync(
            It.Is<ProviderCreditMutationRequestDto>(r =>
                r.ProviderId == providerId &&
                r.EntryType == ProviderCreditLedgerEntryType.Debit &&
                r.Amount == 120m &&
                r.ReferenceType == "ServiceAppointment" &&
                r.ReferenceId == appointmentId &&
                r.Source != null &&
                r.Source.Contains("FinancialPolicy:expire_pending_confirmation", StringComparison.Ordinal)),
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExpirePendingScopeChangeRequestsAsync_ShouldExpireTimedOutPendingScopeChanges()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var scopeChangeId = Guid.NewGuid();

        var serviceRequest = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            Category = ServiceCategory.Electrical,
            Description = "Servico em andamento",
            AddressStreet = "Rua C",
            AddressCity = "Praia Grande",
            AddressZip = "11704150",
            Latitude = -24.01,
            Longitude = -46.41,
            Status = ServiceRequestStatus.InProgress,
            CommercialVersion = 2,
            CommercialState = ServiceRequestCommercialState.PendingClientApproval,
            CommercialBaseValue = 300m,
            CommercialCurrentValue = 300m,
            Proposals =
            {
                new Proposal
                {
                    ProviderId = providerId,
                    Accepted = true,
                    IsInvalidated = false,
                    EstimatedValue = 300m
                }
            }
        };

        var appointment = new ServiceAppointment
        {
            Id = appointmentId,
            ServiceRequestId = requestId,
            ClientId = clientId,
            ProviderId = providerId,
            Status = ServiceAppointmentStatus.InProgress,
            ServiceRequest = serviceRequest
        };

        var scopeChange = new ServiceScopeChangeRequest
        {
            Id = scopeChangeId,
            ServiceRequestId = requestId,
            ServiceAppointmentId = appointmentId,
            ProviderId = providerId,
            Version = 3,
            Status = ServiceScopeChangeRequestStatus.PendingClientApproval,
            Reason = "Escopo adicional",
            AdditionalScopeDescription = "Troca de componente",
            IncrementalValue = 90m,
            RequestedAtUtc = DateTime.UtcNow.AddDays(-2),
            ServiceAppointment = appointment
        };

        _scopeChangeRequestRepositoryMock
            .Setup(r => r.GetExpiredPendingByRequestedAtAsync(It.IsAny<DateTime>(), It.IsAny<int>()))
            .ReturnsAsync(new List<ServiceScopeChangeRequest> { scopeChange });

        _scopeChangeRequestRepositoryMock
            .Setup(r => r.GetByIdAsync(scopeChangeId))
            .ReturnsAsync(scopeChange);

        _commercialValueServiceMock
            .Setup(s => s.RecalculateAsync(It.Is<ServiceRequest>(r => r.Id == requestId)))
            .ReturnsAsync(new ServiceRequestCommercialTotalsDto(300m, 0m, 300m));

        var result = await _service.ExpirePendingScopeChangeRequestsAsync();

        Assert.Equal(1, result);
        _scopeChangeRequestRepositoryMock.Verify(
            r => r.UpdateAsync(It.Is<ServiceScopeChangeRequest>(x =>
                x.Id == scopeChangeId &&
                x.Status == ServiceScopeChangeRequestStatus.Expired &&
                x.ClientResponseReason != null &&
                x.ClientResponseReason.Contains("Expirado automaticamente", StringComparison.OrdinalIgnoreCase))),
            Times.Once);
        _requestRepositoryMock.Verify(
            r => r.UpdateAsync(It.Is<ServiceRequest>(sr =>
                sr.Id == requestId &&
                sr.CommercialState == ServiceRequestCommercialState.Stable)),
            Times.Once);
        _appointmentRepositoryMock.Verify(
            r => r.AddHistoryAsync(It.Is<ServiceAppointmentHistory>(h =>
                h.ServiceAppointmentId == appointmentId &&
                h.Metadata != null &&
                h.Metadata.Contains("scope_change_audit") &&
                h.Metadata.Contains("expired"))),
            Times.Once);
        _notificationServiceMock.Verify(
            n => n.SendNotificationAsync(
                providerId.ToString("N"),
                "Aditivo expirado",
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Once);
        _notificationServiceMock.Verify(
            n => n.SendNotificationAsync(
                clientId.ToString("N"),
                "Aditivo expirado",
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task MarkArrivedAsync_ShouldRequireManualReason_WhenGpsIsUnavailable()
    {
        var providerId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ProviderId = providerId,
                ClientId = Guid.NewGuid(),
                ServiceRequestId = Guid.NewGuid(),
                Status = ServiceAppointmentStatus.Confirmed
            });

        var result = await _service.MarkArrivedAsync(
            providerId,
            UserRole.Provider.ToString(),
            appointmentId,
            new MarkServiceAppointmentArrivalRequestDto(null, null, null, null));

        Assert.False(result.Success);
        Assert.Equal("invalid_reason", result.ErrorCode);
    }

    [Fact]
    public async Task MarkArrivedAsync_ShouldSetArrivedStatus_WhenConfirmedAndGpsProvided()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ProviderId = providerId,
                ClientId = clientId,
                ServiceRequestId = requestId,
                Status = ServiceAppointmentStatus.Confirmed
            });

        var result = await _service.MarkArrivedAsync(
            providerId,
            UserRole.Provider.ToString(),
            appointmentId,
            new MarkServiceAppointmentArrivalRequestDto(-24.01, -46.41, 12.5));

        Assert.True(result.Success, $"{result.ErrorCode} - {result.ErrorMessage}");
        _appointmentRepositoryMock.Verify(r => r.UpdateAsync(It.Is<ServiceAppointment>(a =>
            a.Status == ServiceAppointmentStatus.Arrived &&
            a.ArrivedAtUtc.HasValue &&
            a.ArrivedLatitude == -24.01 &&
            a.ArrivedLongitude == -46.41 &&
            a.ArrivedAccuracyMeters == 12.5 &&
            string.IsNullOrWhiteSpace(a.ArrivedManualReason))), Times.Once);
        _appointmentRepositoryMock.Verify(r => r.AddHistoryAsync(It.Is<ServiceAppointmentHistory>(h =>
            h.NewStatus == ServiceAppointmentStatus.Arrived)), Times.Once);
    }

    [Fact]
    public async Task StartExecutionAsync_ShouldSetInProgressAndRequestInProgress_WhenArrivalWasRegistered()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var request = BuildRequest(clientId, providerId, acceptedProposal: true);
        request.Id = requestId;
        request.Status = ServiceRequestStatus.Scheduled;

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ProviderId = providerId,
                ClientId = clientId,
                ServiceRequestId = requestId,
                Status = ServiceAppointmentStatus.Arrived,
                ArrivedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                ServiceRequest = request
            });

        var result = await _service.StartExecutionAsync(
            providerId,
            UserRole.Provider.ToString(),
            appointmentId,
            new StartServiceAppointmentExecutionRequestDto("Inicio registrado"));

        Assert.True(result.Success, $"{result.ErrorCode} - {result.ErrorMessage}");
        _appointmentRepositoryMock.Verify(r => r.UpdateAsync(It.Is<ServiceAppointment>(a =>
            a.Status == ServiceAppointmentStatus.InProgress &&
            a.StartedAtUtc.HasValue)), Times.Once);
        _appointmentRepositoryMock.Verify(r => r.AddHistoryAsync(It.Is<ServiceAppointmentHistory>(h =>
            h.NewStatus == ServiceAppointmentStatus.InProgress)), Times.Once);
        _requestRepositoryMock.Verify(r => r.UpdateAsync(It.Is<ServiceRequest>(sr =>
            sr.Id == requestId && sr.Status == ServiceRequestStatus.InProgress)), Times.Once);
    }

    [Fact]
    public async Task UpdateOperationalStatusAsync_ShouldReturnInvalidTransition_WhenSkippingStages()
    {
        var providerId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ProviderId = providerId,
                ClientId = Guid.NewGuid(),
                ServiceRequestId = Guid.NewGuid(),
                Status = ServiceAppointmentStatus.Confirmed
            });

        var result = await _service.UpdateOperationalStatusAsync(
            providerId,
            UserRole.Provider.ToString(),
            appointmentId,
            new UpdateServiceAppointmentOperationalStatusRequestDto("InService", "Tentativa de pulo"));

        Assert.False(result.Success);
        Assert.Equal("invalid_operational_transition", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateOperationalStatusAsync_ShouldRequireReason_WhenWaitingParts()
    {
        var providerId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ProviderId = providerId,
                ClientId = Guid.NewGuid(),
                ServiceRequestId = Guid.NewGuid(),
                Status = ServiceAppointmentStatus.InProgress,
                StartedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                OperationalStatus = ServiceAppointmentOperationalStatus.InService
            });

        var result = await _service.UpdateOperationalStatusAsync(
            providerId,
            UserRole.Provider.ToString(),
            appointmentId,
            new UpdateServiceAppointmentOperationalStatusRequestDto("WaitingParts", null));

        Assert.False(result.Success);
        Assert.Equal("invalid_reason", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateOperationalStatusAsync_ShouldUpdateStatus_WhenTransitionIsValid()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var request = BuildRequest(clientId, providerId, acceptedProposal: true);
        request.Id = requestId;
        request.Status = ServiceRequestStatus.InProgress;

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ProviderId = providerId,
                ClientId = clientId,
                ServiceRequestId = requestId,
                Status = ServiceAppointmentStatus.InProgress,
                ArrivedAtUtc = DateTime.UtcNow.AddMinutes(-20),
                StartedAtUtc = DateTime.UtcNow.AddMinutes(-15),
                OperationalStatus = ServiceAppointmentOperationalStatus.InService,
                ServiceRequest = request
            });

        var result = await _service.UpdateOperationalStatusAsync(
            providerId,
            UserRole.Provider.ToString(),
            appointmentId,
            new UpdateServiceAppointmentOperationalStatusRequestDto("WaitingParts", "Aguardando reposicao"));

        Assert.True(result.Success, $"{result.ErrorCode} - {result.ErrorMessage}");
        _appointmentRepositoryMock.Verify(r => r.UpdateAsync(It.Is<ServiceAppointment>(a =>
            a.OperationalStatus == ServiceAppointmentOperationalStatus.WaitingParts &&
            a.Status == ServiceAppointmentStatus.InProgress &&
            a.OperationalStatusUpdatedAtUtc.HasValue &&
            a.OperationalStatusReason == "Aguardando reposicao")), Times.Once);
        _appointmentRepositoryMock.Verify(r => r.AddHistoryAsync(It.Is<ServiceAppointmentHistory>(h =>
            h.NewOperationalStatus == ServiceAppointmentOperationalStatus.WaitingParts &&
            h.PreviousOperationalStatus == ServiceAppointmentOperationalStatus.InService)), Times.Once);
    }

    [Fact]
    public async Task UpdateOperationalStatusAsync_ShouldBlockCompletion_WhenChecklistHasPendingRequiredItems()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var request = BuildRequest(clientId, providerId, acceptedProposal: true);
        request.Id = requestId;
        request.Status = ServiceRequestStatus.InProgress;

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ProviderId = providerId,
                ClientId = clientId,
                ServiceRequestId = requestId,
                Status = ServiceAppointmentStatus.InProgress,
                ArrivedAtUtc = DateTime.UtcNow.AddMinutes(-20),
                StartedAtUtc = DateTime.UtcNow.AddMinutes(-15),
                OperationalStatus = ServiceAppointmentOperationalStatus.InService,
                ServiceRequest = request
            });

        _checklistServiceMock
            .Setup(s => s.ValidateRequiredItemsForCompletionAsync(appointmentId, UserRole.Provider.ToString()))
            .ReturnsAsync(new ServiceAppointmentChecklistValidationResultDto(
                true,
                false,
                2,
                new[] { "Desligar energia", "Validar aterramento" }));

        var result = await _service.UpdateOperationalStatusAsync(
            providerId,
            UserRole.Provider.ToString(),
            appointmentId,
            new UpdateServiceAppointmentOperationalStatusRequestDto("Completed", "Finalizado"));

        Assert.False(result.Success);
        Assert.Equal("required_checklist_pending", result.ErrorCode);
        _appointmentRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<ServiceAppointment>()), Times.Never);
        _requestRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<ServiceRequest>()), Times.Never);
    }

    [Fact]
    public async Task UpdateOperationalStatusAsync_ShouldBlockCompletion_WhenScopeChangeIsPending()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var request = BuildRequest(clientId, providerId, acceptedProposal: true);
        request.Id = requestId;
        request.Status = ServiceRequestStatus.InProgress;

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ProviderId = providerId,
                ClientId = clientId,
                ServiceRequestId = requestId,
                Status = ServiceAppointmentStatus.InProgress,
                ArrivedAtUtc = DateTime.UtcNow.AddMinutes(-20),
                StartedAtUtc = DateTime.UtcNow.AddMinutes(-15),
                OperationalStatus = ServiceAppointmentOperationalStatus.InService,
                ServiceRequest = request
            });

        _checklistServiceMock
            .Setup(s => s.ValidateRequiredItemsForCompletionAsync(appointmentId, UserRole.Provider.ToString()))
            .ReturnsAsync(new ServiceAppointmentChecklistValidationResultDto(
                true,
                true,
                0,
                Array.Empty<string>()));

        _scopeChangeRequestRepositoryMock
            .Setup(r => r.GetLatestByAppointmentIdAndStatusAsync(
                appointmentId,
                ServiceScopeChangeRequestStatus.PendingClientApproval))
            .ReturnsAsync(new ServiceScopeChangeRequest
            {
                Id = Guid.NewGuid(),
                ServiceAppointmentId = appointmentId,
                ServiceRequestId = requestId,
                ProviderId = providerId,
                Version = 1,
                Status = ServiceScopeChangeRequestStatus.PendingClientApproval,
                Reason = "Aditivo aguardando cliente",
                AdditionalScopeDescription = "Troca de componente adicional",
                IncrementalValue = 90m,
                RequestedAtUtc = DateTime.UtcNow.AddMinutes(-3)
            });

        var result = await _service.UpdateOperationalStatusAsync(
            providerId,
            UserRole.Provider.ToString(),
            appointmentId,
            new UpdateServiceAppointmentOperationalStatusRequestDto("Completed", "Tentativa de concluir"));

        Assert.False(result.Success);
        Assert.Equal("scope_change_pending", result.ErrorCode);
        _appointmentRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<ServiceAppointment>()), Times.Never);
        _requestRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<ServiceRequest>()), Times.Never);
        _completionTermRepositoryMock.Verify(r => r.AddAsync(It.IsAny<ServiceCompletionTerm>()), Times.Never);
    }

    [Fact]
    public async Task UpdateOperationalStatusAsync_ShouldSetPendingClientCompletionAcceptance_WhenChecklistIsReady()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var request = BuildRequest(clientId, providerId, acceptedProposal: true);
        request.Id = requestId;
        request.Status = ServiceRequestStatus.InProgress;

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ProviderId = providerId,
                ClientId = clientId,
                ServiceRequestId = requestId,
                Status = ServiceAppointmentStatus.InProgress,
                ArrivedAtUtc = DateTime.UtcNow.AddMinutes(-20),
                StartedAtUtc = DateTime.UtcNow.AddMinutes(-15),
                OperationalStatus = ServiceAppointmentOperationalStatus.InService,
                ServiceRequest = request
            });

        _checklistServiceMock
            .Setup(s => s.ValidateRequiredItemsForCompletionAsync(appointmentId, UserRole.Provider.ToString()))
            .ReturnsAsync(new ServiceAppointmentChecklistValidationResultDto(
                true,
                true,
                0,
                Array.Empty<string>()));

        var result = await _service.UpdateOperationalStatusAsync(
            providerId,
            UserRole.Provider.ToString(),
            appointmentId,
            new UpdateServiceAppointmentOperationalStatusRequestDto("Completed", "Atendimento encerrado"));

        Assert.True(result.Success, $"{result.ErrorCode} - {result.ErrorMessage}");
        _appointmentRepositoryMock.Verify(r => r.UpdateAsync(It.Is<ServiceAppointment>(a =>
            a.Status == ServiceAppointmentStatus.Completed &&
            a.OperationalStatus == ServiceAppointmentOperationalStatus.Completed &&
            a.CompletedAtUtc.HasValue &&
            a.OperationalStatusUpdatedAtUtc.HasValue)), Times.Once);
        _requestRepositoryMock.Verify(r => r.UpdateAsync(It.Is<ServiceRequest>(sr =>
            sr.Id == requestId &&
            sr.Status == ServiceRequestStatus.PendingClientCompletionAcceptance)), Times.Once);
        _completionTermRepositoryMock.Verify(r => r.AddAsync(It.Is<ServiceCompletionTerm>(t =>
            t.ServiceAppointmentId == appointmentId &&
            t.ServiceRequestId == requestId &&
            t.Status == ServiceCompletionTermStatus.PendingClientAcceptance &&
            !string.IsNullOrWhiteSpace(t.AcceptancePinHashSha256) &&
            t.AcceptancePinExpiresAtUtc.HasValue)), Times.Once);
    }

    [Fact]
    public async Task GenerateCompletionPinAsync_ShouldCreateTermAndReturnOneTimePin()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var request = BuildRequest(clientId, providerId, acceptedProposal: true);
        request.Id = requestId;
        request.Status = ServiceRequestStatus.PendingClientCompletionAcceptance;

        var appointment = new ServiceAppointment
        {
            Id = appointmentId,
            ProviderId = providerId,
            ClientId = clientId,
            ServiceRequestId = requestId,
            Status = ServiceAppointmentStatus.Completed,
            CompletedAtUtc = DateTime.UtcNow,
            ServiceRequest = request
        };

        ServiceCompletionTerm? persistedTerm = null;
        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(appointment);

        _completionTermRepositoryMock
            .Setup(r => r.GetByAppointmentIdAsync(appointmentId))
            .ReturnsAsync(() => persistedTerm);

        _completionTermRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ServiceCompletionTerm>()))
            .Callback<ServiceCompletionTerm>(term => persistedTerm = term)
            .Returns(Task.CompletedTask);

        var result = await _service.GenerateCompletionPinAsync(
            providerId,
            UserRole.Provider.ToString(),
            appointmentId,
            new GenerateServiceCompletionPinRequestDto());

        Assert.True(result.Success, $"{result.ErrorCode} - {result.ErrorMessage}");
        Assert.NotNull(result.Term);
        Assert.False(string.IsNullOrWhiteSpace(result.OneTimePin));
        Assert.Equal(6, result.OneTimePin!.Length);
        Assert.NotNull(persistedTerm);
        Assert.Equal(ServiceCompletionTermStatus.PendingClientAcceptance, persistedTerm!.Status);
        Assert.False(string.IsNullOrWhiteSpace(persistedTerm.AcceptancePinHashSha256));
        Assert.True(persistedTerm.AcceptancePinExpiresAtUtc.HasValue);
    }

    [Fact]
    public async Task ValidateCompletionPinAsync_ShouldAcceptTermAndCompleteRequest_WhenPinMatches()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var request = BuildRequest(clientId, providerId, acceptedProposal: true);
        request.Id = requestId;
        request.Status = ServiceRequestStatus.PendingClientCompletionAcceptance;

        var appointment = new ServiceAppointment
        {
            Id = appointmentId,
            ProviderId = providerId,
            ClientId = clientId,
            ServiceRequestId = requestId,
            Status = ServiceAppointmentStatus.Completed,
            CompletedAtUtc = DateTime.UtcNow,
            ServiceRequest = request
        };

        ServiceCompletionTerm? persistedTerm = null;
        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(appointment);

        _completionTermRepositoryMock
            .Setup(r => r.GetByAppointmentIdAsync(appointmentId))
            .ReturnsAsync(() => persistedTerm);

        _completionTermRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ServiceCompletionTerm>()))
            .Callback<ServiceCompletionTerm>(term => persistedTerm = term)
            .Returns(Task.CompletedTask);

        _completionTermRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<ServiceCompletionTerm>()))
            .Callback<ServiceCompletionTerm>(term => persistedTerm = term)
            .Returns(Task.CompletedTask);

        var generated = await _service.GenerateCompletionPinAsync(
            providerId,
            UserRole.Provider.ToString(),
            appointmentId,
            new GenerateServiceCompletionPinRequestDto());

        Assert.True(generated.Success);
        Assert.False(string.IsNullOrWhiteSpace(generated.OneTimePin));

        var validated = await _service.ValidateCompletionPinAsync(
            clientId,
            UserRole.Client.ToString(),
            appointmentId,
            new ValidateServiceCompletionPinRequestDto(generated.OneTimePin!));

        Assert.True(validated.Success, $"{validated.ErrorCode} - {validated.ErrorMessage}");
        Assert.NotNull(validated.Term);
        Assert.Equal(ServiceCompletionTermStatus.AcceptedByClient.ToString(), validated.Term!.Status);
        Assert.Equal(ServiceCompletionAcceptanceMethod.Pin.ToString(), validated.Term.AcceptedWithMethod);
        Assert.NotNull(persistedTerm);
        Assert.Equal(ServiceCompletionTermStatus.AcceptedByClient, persistedTerm!.Status);
        Assert.Equal(ServiceCompletionAcceptanceMethod.Pin, persistedTerm.AcceptedWithMethod);
        Assert.Null(persistedTerm.AcceptancePinHashSha256);
        Assert.Equal(ServiceRequestStatus.Completed, request.Status);
        _requestRepositoryMock.Verify(r => r.UpdateAsync(It.Is<ServiceRequest>(sr =>
            sr.Id == requestId &&
            sr.Status == ServiceRequestStatus.Completed)), Times.Once);
    }

    [Fact]
    public async Task ValidateCompletionPinAsync_ShouldRejectReplayAttempt_AfterPinAlreadyUsed()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var request = BuildRequest(clientId, providerId, acceptedProposal: true);
        request.Id = requestId;
        request.Status = ServiceRequestStatus.PendingClientCompletionAcceptance;

        var appointment = new ServiceAppointment
        {
            Id = appointmentId,
            ProviderId = providerId,
            ClientId = clientId,
            ServiceRequestId = requestId,
            Status = ServiceAppointmentStatus.Completed,
            CompletedAtUtc = DateTime.UtcNow,
            ServiceRequest = request
        };

        ServiceCompletionTerm? persistedTerm = null;
        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(appointment);

        _completionTermRepositoryMock
            .Setup(r => r.GetByAppointmentIdAsync(appointmentId))
            .ReturnsAsync(() => persistedTerm);

        _completionTermRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ServiceCompletionTerm>()))
            .Callback<ServiceCompletionTerm>(term => persistedTerm = term)
            .Returns(Task.CompletedTask);

        _completionTermRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<ServiceCompletionTerm>()))
            .Callback<ServiceCompletionTerm>(term => persistedTerm = term)
            .Returns(Task.CompletedTask);

        var generated = await _service.GenerateCompletionPinAsync(
            providerId,
            UserRole.Provider.ToString(),
            appointmentId,
            new GenerateServiceCompletionPinRequestDto());

        Assert.True(generated.Success);
        Assert.False(string.IsNullOrWhiteSpace(generated.OneTimePin));

        var firstAttempt = await _service.ValidateCompletionPinAsync(
            clientId,
            UserRole.Client.ToString(),
            appointmentId,
            new ValidateServiceCompletionPinRequestDto(generated.OneTimePin!));

        Assert.True(firstAttempt.Success, $"{firstAttempt.ErrorCode} - {firstAttempt.ErrorMessage}");

        var replayAttempt = await _service.ValidateCompletionPinAsync(
            clientId,
            UserRole.Client.ToString(),
            appointmentId,
            new ValidateServiceCompletionPinRequestDto(generated.OneTimePin!));

        Assert.False(replayAttempt.Success);
        Assert.Equal("invalid_state", replayAttempt.ErrorCode);
        Assert.NotNull(replayAttempt.Term);
        Assert.Equal(ServiceCompletionTermStatus.AcceptedByClient.ToString(), replayAttempt.Term!.Status);
        _requestRepositoryMock.Verify(r => r.UpdateAsync(It.Is<ServiceRequest>(sr =>
            sr.Id == requestId &&
            sr.Status == ServiceRequestStatus.Completed)), Times.Once);
    }

    [Fact]
    public async Task ConfirmCompletionAsync_ShouldAcceptWithSignature_WhenPendingTermExists()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var request = BuildRequest(clientId, providerId, acceptedProposal: true);
        request.Id = requestId;
        request.Status = ServiceRequestStatus.PendingClientCompletionAcceptance;

        var appointment = new ServiceAppointment
        {
            Id = appointmentId,
            ProviderId = providerId,
            ClientId = clientId,
            ServiceRequestId = requestId,
            Status = ServiceAppointmentStatus.Completed,
            CompletedAtUtc = DateTime.UtcNow,
            ServiceRequest = request
        };

        var term = new ServiceCompletionTerm
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            ServiceAppointmentId = appointmentId,
            ProviderId = providerId,
            ClientId = clientId,
            Status = ServiceCompletionTermStatus.PendingClientAcceptance,
            AcceptancePinHashSha256 = "hash",
            AcceptancePinExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
            Summary = "Resumo",
            PayloadHashSha256 = "payload-hash"
        };

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(appointment);

        _completionTermRepositoryMock
            .Setup(r => r.GetByAppointmentIdAsync(appointmentId))
            .ReturnsAsync(term);

        _completionTermRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<ServiceCompletionTerm>()))
            .Callback<ServiceCompletionTerm>(updated =>
            {
                term.Status = updated.Status;
                term.AcceptedWithMethod = updated.AcceptedWithMethod;
                term.AcceptedSignatureName = updated.AcceptedSignatureName;
                term.AcceptedAtUtc = updated.AcceptedAtUtc;
            })
            .Returns(Task.CompletedTask);

        var result = await _service.ConfirmCompletionAsync(
            clientId,
            UserRole.Client.ToString(),
            appointmentId,
            new ConfirmServiceCompletionRequestDto("SignatureName", SignatureName: "Cliente 02"));

        Assert.True(result.Success, $"{result.ErrorCode} - {result.ErrorMessage}");
        Assert.NotNull(result.Term);
        Assert.Equal(ServiceCompletionTermStatus.AcceptedByClient.ToString(), result.Term!.Status);
        Assert.Equal(ServiceCompletionAcceptanceMethod.SignatureName.ToString(), result.Term.AcceptedWithMethod);
        Assert.Equal(ServiceCompletionTermStatus.AcceptedByClient, term.Status);
        Assert.Equal(ServiceCompletionAcceptanceMethod.SignatureName, term.AcceptedWithMethod);
        Assert.Equal("Cliente 02", term.AcceptedSignatureName);
        Assert.Equal(ServiceRequestStatus.Completed, request.Status);
        _requestRepositoryMock.Verify(r => r.UpdateAsync(It.Is<ServiceRequest>(sr =>
            sr.Id == requestId &&
            sr.Status == ServiceRequestStatus.Completed)), Times.Once);
    }

    [Fact]
    public async Task ConfirmCompletionAsync_ShouldReturnInvalidMethod_WhenMethodIsUnsupported()
    {
        var result = await _service.ConfirmCompletionAsync(
            Guid.NewGuid(),
            UserRole.Client.ToString(),
            Guid.NewGuid(),
            new ConfirmServiceCompletionRequestDto("Biometria"));

        Assert.False(result.Success);
        Assert.Equal("invalid_acceptance_method", result.ErrorCode);
    }

    [Fact]
    public async Task ContestCompletionAsync_ShouldMarkTermAsContested_WhenReasonIsValid()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var request = BuildRequest(clientId, providerId, acceptedProposal: true);
        request.Id = requestId;
        request.Status = ServiceRequestStatus.PendingClientCompletionAcceptance;

        var appointment = new ServiceAppointment
        {
            Id = appointmentId,
            ProviderId = providerId,
            ClientId = clientId,
            ServiceRequestId = requestId,
            Status = ServiceAppointmentStatus.Completed,
            CompletedAtUtc = DateTime.UtcNow,
            ServiceRequest = request
        };

        var term = new ServiceCompletionTerm
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            ServiceAppointmentId = appointmentId,
            ProviderId = providerId,
            ClientId = clientId,
            Status = ServiceCompletionTermStatus.PendingClientAcceptance,
            AcceptancePinHashSha256 = "hash",
            AcceptancePinExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
            Summary = "Resumo",
            PayloadHashSha256 = "payload-hash"
        };

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(appointment);

        _completionTermRepositoryMock
            .Setup(r => r.GetByAppointmentIdAsync(appointmentId))
            .ReturnsAsync(term);

        _completionTermRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<ServiceCompletionTerm>()))
            .Callback<ServiceCompletionTerm>(updated =>
            {
                term.Status = updated.Status;
                term.ContestReason = updated.ContestReason;
                term.ContestedAtUtc = updated.ContestedAtUtc;
                term.AcceptancePinHashSha256 = updated.AcceptancePinHashSha256;
                term.AcceptancePinExpiresAtUtc = updated.AcceptancePinExpiresAtUtc;
            })
            .Returns(Task.CompletedTask);

        var result = await _service.ContestCompletionAsync(
            clientId,
            UserRole.Client.ToString(),
            appointmentId,
            new ContestServiceCompletionRequestDto("Servico nao foi concluido corretamente."));

        Assert.True(result.Success, $"{result.ErrorCode} - {result.ErrorMessage}");
        Assert.NotNull(result.Term);
        Assert.Equal(ServiceCompletionTermStatus.ContestedByClient.ToString(), result.Term!.Status);
        Assert.Equal(ServiceCompletionTermStatus.ContestedByClient, term.Status);
        Assert.Equal("Servico nao foi concluido corretamente.", term.ContestReason);
        Assert.NotNull(term.ContestedAtUtc);
        Assert.Null(term.AcceptancePinHashSha256);
        Assert.Null(term.AcceptancePinExpiresAtUtc);
        _requestRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<ServiceRequest>()), Times.Never);
    }

    [Fact]
    public async Task ContestCompletionAsync_ShouldRejectShortReason()
    {
        var result = await _service.ContestCompletionAsync(
            Guid.NewGuid(),
            UserRole.Client.ToString(),
            Guid.NewGuid(),
            new ContestServiceCompletionRequestDto("abc"));

        Assert.False(result.Success);
        Assert.Equal("contest_reason_required", result.ErrorCode);
    }

    [Fact]
    public async Task ContestCompletionAsync_ShouldNotifyActiveAdmins()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var activeAdminId = Guid.NewGuid();

        _userRepositoryMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[]
            {
                new User { Id = activeAdminId, Role = UserRole.Admin, IsActive = true },
                new User { Id = Guid.NewGuid(), Role = UserRole.Admin, IsActive = false },
                new User { Id = Guid.NewGuid(), Role = UserRole.Provider, IsActive = true }
            });

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ProviderId = providerId,
                ClientId = clientId,
                ServiceRequestId = requestId,
                Status = ServiceAppointmentStatus.Completed
            });

        _completionTermRepositoryMock
            .Setup(r => r.GetByAppointmentIdAsync(appointmentId))
            .ReturnsAsync(new ServiceCompletionTerm
            {
                Id = Guid.NewGuid(),
                ServiceAppointmentId = appointmentId,
                ServiceRequestId = requestId,
                ProviderId = providerId,
                ClientId = clientId,
                Status = ServiceCompletionTermStatus.PendingClientAcceptance,
                Summary = "Resumo",
                PayloadHashSha256 = "payload"
            });

        var result = await _service.ContestCompletionAsync(
            clientId,
            UserRole.Client.ToString(),
            appointmentId,
            new ContestServiceCompletionRequestDto("Cliente reportou divergencia."));

        Assert.True(result.Success, $"{result.ErrorCode} - {result.ErrorMessage}");
        _notificationServiceMock.Verify(n => n.SendNotificationAsync(
                activeAdminId.ToString("N"),
                "Agendamento: contestacao para analise",
                It.Is<string>(m => m.Contains("Motivo:", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task GetCompletionTermAsync_ShouldReturnTerm_WhenClientOwnsAppointment()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var appointment = new ServiceAppointment
        {
            Id = appointmentId,
            ProviderId = providerId,
            ClientId = clientId,
            ServiceRequestId = requestId,
            Status = ServiceAppointmentStatus.Completed
        };

        var term = new ServiceCompletionTerm
        {
            Id = Guid.NewGuid(),
            ServiceAppointmentId = appointmentId,
            ServiceRequestId = requestId,
            ProviderId = providerId,
            ClientId = clientId,
            Status = ServiceCompletionTermStatus.AcceptedByClient,
            Summary = "Resumo do atendimento",
            PayloadHashSha256 = "payload"
        };

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(appointment);

        _completionTermRepositoryMock
            .Setup(r => r.GetByAppointmentIdAsync(appointmentId))
            .ReturnsAsync(term);

        var result = await _service.GetCompletionTermAsync(
            clientId,
            UserRole.Client.ToString(),
            appointmentId);

        Assert.True(result.Success, $"{result.ErrorCode} - {result.ErrorMessage}");
        Assert.NotNull(result.Term);
        Assert.Equal(ServiceCompletionTermStatus.AcceptedByClient.ToString(), result.Term!.Status);
        Assert.Equal("Resumo do atendimento", result.Term.Summary);
    }

    [Fact]
    public async Task GetCompletionTermAsync_ShouldReturnForbidden_WhenClientIsNotOwner()
    {
        var providerId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ProviderId = providerId,
                ClientId = Guid.NewGuid(),
                ServiceRequestId = Guid.NewGuid(),
                Status = ServiceAppointmentStatus.Completed
            });

        var result = await _service.GetCompletionTermAsync(
            Guid.NewGuid(),
            UserRole.Client.ToString(),
            appointmentId);

        Assert.False(result.Success);
        Assert.Equal("forbidden", result.ErrorCode);
    }

    [Fact]
    public async Task CreateWarrantyClaimAsync_ShouldReturnForbidden_WhenActorIsProvider()
    {
        var result = await _service.CreateWarrantyClaimAsync(
            Guid.NewGuid(),
            UserRole.Provider.ToString(),
            Guid.NewGuid(),
            new CreateServiceWarrantyClaimRequestDto("Equipamento voltou a falhar."));

        Assert.False(result.Success);
        Assert.Equal("forbidden", result.ErrorCode);
    }

    [Fact]
    public async Task CreateWarrantyClaimAsync_ShouldCreateClaim_WhenClientOwnsCompletedAppointment()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var completedAtUtc = DateTime.UtcNow.AddDays(-2);

        var serviceRequest = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            Category = ServiceCategory.Plumbing,
            Status = ServiceRequestStatus.Validated,
            Description = "Servico concluido",
            AddressStreet = "Rua D",
            AddressCity = "Praia Grande",
            AddressZip = "11704150",
            Latitude = -24.01,
            Longitude = -46.41
        };

        var appointment = new ServiceAppointment
        {
            Id = appointmentId,
            ServiceRequestId = requestId,
            ClientId = clientId,
            ProviderId = providerId,
            Status = ServiceAppointmentStatus.Completed,
            CompletedAtUtc = completedAtUtc,
            WindowStartUtc = completedAtUtc.AddHours(-1),
            WindowEndUtc = completedAtUtc,
            ServiceRequest = serviceRequest
        };

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(appointment);

        _serviceWarrantyClaimRepositoryMock
            .Setup(r => r.GetByAppointmentIdAsync(appointmentId))
            .ReturnsAsync(Array.Empty<ServiceWarrantyClaim>());

        ServiceWarrantyClaim? createdClaim = null;
        _serviceWarrantyClaimRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ServiceWarrantyClaim>()))
            .Callback<ServiceWarrantyClaim>(claim =>
            {
                claim.Id = Guid.NewGuid();
                createdClaim = claim;
            })
            .Returns(Task.CompletedTask);

        var result = await _service.CreateWarrantyClaimAsync(
            clientId,
            UserRole.Client.ToString(),
            appointmentId,
            new CreateServiceWarrantyClaimRequestDto("Vazamento retornou na mesma tubulacao."));

        Assert.True(result.Success, $"{result.ErrorCode} - {result.ErrorMessage}");
        Assert.NotNull(result.WarrantyClaim);
        Assert.NotNull(createdClaim);
        Assert.Equal(ServiceWarrantyClaimStatus.PendingProviderReview, createdClaim!.Status);
        Assert.Equal("Vazamento retornou na mesma tubulacao.", createdClaim.IssueDescription);
        Assert.Equal(createdClaim.Id, result.WarrantyClaim!.Id);
        Assert.Equal(requestId, result.WarrantyClaim.ServiceRequestId);

        _serviceWarrantyClaimRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<ServiceWarrantyClaim>()),
            Times.Once);

        _appointmentRepositoryMock.Verify(
            r => r.AddHistoryAsync(It.Is<ServiceAppointmentHistory>(h =>
                h.ServiceAppointmentId == appointmentId &&
                h.Metadata != null &&
                h.Metadata.Contains("WarrantyClaimCreated"))),
            Times.Once);

        _notificationServiceMock.Verify(
            n => n.SendNotificationAsync(
                providerId.ToString("N"),
                "Nova solicitacao de garantia",
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ScheduleWarrantyRevisitAsync_ShouldReturnForbidden_WhenActorIsClient()
    {
        var result = await _service.ScheduleWarrantyRevisitAsync(
            Guid.NewGuid(),
            UserRole.Client.ToString(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new ScheduleServiceWarrantyRevisitRequestDto(
                DateTime.UtcNow.AddDays(1),
                DateTime.UtcNow.AddDays(1).AddHours(1),
                "Teste"));

        Assert.False(result.Success);
        Assert.Equal("forbidden", result.ErrorCode);
    }

    [Fact]
    public async Task ScheduleWarrantyRevisitAsync_ShouldCreateConfirmedAppointmentAndLinkClaim_WhenSlotIsAvailable()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var warrantyClaimId = Guid.NewGuid();
        var revisitStartUtc = DateTime.UtcNow.AddDays(1).Date.AddHours(13);
        var revisitEndUtc = revisitStartUtc.AddHours(1);

        var originalAppointment = new ServiceAppointment
        {
            Id = appointmentId,
            ServiceRequestId = requestId,
            ClientId = clientId,
            ProviderId = providerId,
            Status = ServiceAppointmentStatus.Completed,
            WindowStartUtc = DateTime.UtcNow.AddDays(-3),
            WindowEndUtc = DateTime.UtcNow.AddDays(-3).AddHours(1),
            CompletedAtUtc = DateTime.UtcNow.AddDays(-3)
        };

        var warrantyClaim = new ServiceWarrantyClaim
        {
            Id = warrantyClaimId,
            ServiceRequestId = requestId,
            ServiceAppointmentId = appointmentId,
            ClientId = clientId,
            ProviderId = providerId,
            Status = ServiceWarrantyClaimStatus.PendingProviderReview,
            IssueDescription = "Falha voltou",
            RequestedAtUtc = DateTime.UtcNow.AddDays(-1),
            WarrantyWindowEndsAtUtc = DateTime.UtcNow.AddDays(10),
            ProviderResponseDueAtUtc = DateTime.UtcNow.AddDays(1)
        };

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(originalAppointment);

        _serviceWarrantyClaimRepositoryMock
            .Setup(r => r.GetByIdAsync(warrantyClaimId))
            .ReturnsAsync(warrantyClaim);

        _appointmentRepositoryMock
            .Setup(r => r.GetAvailabilityRulesByProviderAsync(providerId))
            .ReturnsAsync(new List<ProviderAvailabilityRule>
            {
                new()
                {
                    ProviderId = providerId,
                    DayOfWeek = revisitStartUtc.DayOfWeek,
                    StartTime = TimeSpan.FromHours(8),
                    EndTime = TimeSpan.FromHours(18),
                    SlotDurationMinutes = 30,
                    IsActive = true
                }
            });

        _appointmentRepositoryMock
            .Setup(r => r.GetAvailabilityExceptionsByProviderAsync(
                providerId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync(Array.Empty<ProviderAvailabilityException>());

        _appointmentRepositoryMock
            .Setup(r => r.GetProviderAppointmentsByStatusesInRangeAsync(
                providerId,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<IReadOnlyCollection<ServiceAppointmentStatus>>()))
            .ReturnsAsync(Array.Empty<ServiceAppointment>());

        _appointmentRepositoryMock
            .Setup(r => r.GetByRequestIdAsync(requestId))
            .ReturnsAsync(Array.Empty<ServiceAppointment>());

        ServiceAppointment? createdRevisit = null;
        _appointmentRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ServiceAppointment>()))
            .Callback<ServiceAppointment>(appointment =>
            {
                appointment.Id = Guid.NewGuid();
                createdRevisit = appointment;
            })
            .Returns(Task.CompletedTask);

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(It.Is<Guid>(id => createdRevisit != null && id == createdRevisit.Id)))
            .ReturnsAsync(() => createdRevisit);

        var result = await _service.ScheduleWarrantyRevisitAsync(
            providerId,
            UserRole.Provider.ToString(),
            appointmentId,
            warrantyClaimId,
            new ScheduleServiceWarrantyRevisitRequestDto(
                revisitStartUtc,
                revisitEndUtc,
                "Cliente pediu retorno no periodo da tarde."));

        Assert.True(result.Success, $"{result.ErrorCode} - {result.ErrorMessage}");
        Assert.NotNull(result.WarrantyClaim);
        Assert.NotNull(result.RevisitAppointment);
        Assert.NotNull(createdRevisit);
        Assert.Equal(ServiceAppointmentStatus.Confirmed, createdRevisit!.Status);
        Assert.Equal(ServiceWarrantyClaimStatus.RevisitScheduled.ToString(), result.WarrantyClaim!.Status);
        Assert.Equal(createdRevisit.Id, result.WarrantyClaim.RevisitAppointmentId);
        Assert.Equal(createdRevisit.Id, result.RevisitAppointment!.Id);

        _serviceWarrantyClaimRepositoryMock.Verify(
            r => r.UpdateAsync(It.Is<ServiceWarrantyClaim>(c =>
                c.Id == warrantyClaimId &&
                c.Status == ServiceWarrantyClaimStatus.RevisitScheduled &&
                c.RevisitAppointmentId.HasValue)),
            Times.Once);
    }

    [Fact]
    public async Task CreateScopeChangeRequestAsync_ShouldReturnForbidden_WhenActorIsClient()
    {
        var result = await _service.CreateScopeChangeRequestAsync(
            Guid.NewGuid(),
            UserRole.Client.ToString(),
            Guid.NewGuid(),
            new CreateServiceScopeChangeRequestDto(
                "Escopo maior do que o previsto",
                "Necessario trocar toda a fiacao do quadro",
                120m));

        Assert.False(result.Success);
        Assert.Equal("forbidden", result.ErrorCode);
    }

    [Fact]
    public async Task CreateScopeChangeRequestAsync_ShouldCreatePendingRequest_WhenProviderOwnsAppointment()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var previousVersionId = Guid.NewGuid();
        var nowUtc = DateTime.UtcNow;

        var appointment = new ServiceAppointment
        {
            Id = appointmentId,
            ProviderId = providerId,
            ClientId = clientId,
            ServiceRequestId = requestId,
            Status = ServiceAppointmentStatus.InProgress,
            ServiceRequest = new ServiceRequest
            {
                Id = requestId,
                ClientId = clientId,
                Category = ServiceCategory.Electrical,
                Status = ServiceRequestStatus.InProgress,
                Description = "Servico em andamento",
                AddressStreet = "Rua A",
                AddressCity = "Praia Grande",
                AddressZip = "11704150",
                Latitude = -24.01,
                Longitude = -46.41,
                Proposals =
                {
                    new Proposal
                    {
                        ProviderId = providerId,
                        Accepted = true,
                        IsInvalidated = false,
                        EstimatedValue = 600m
                    }
                }
            }
        };

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(providerId))
            .ReturnsAsync(new User
            {
                Id = providerId,
                Role = UserRole.Provider,
                IsActive = true,
                ProviderProfile = new ProviderProfile
                {
                    Plan = ProviderPlan.Silver
                }
            });

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(appointment);

        _scopeChangeRequestRepositoryMock
            .Setup(r => r.GetLatestByAppointmentIdAndStatusAsync(
                appointmentId,
                ServiceScopeChangeRequestStatus.PendingClientApproval))
            .ReturnsAsync((ServiceScopeChangeRequest?)null);

        _scopeChangeRequestRepositoryMock
            .Setup(r => r.GetLatestByAppointmentIdAsync(appointmentId))
            .ReturnsAsync(new ServiceScopeChangeRequest
            {
                Id = previousVersionId,
                ServiceAppointmentId = appointmentId,
                ServiceRequestId = requestId,
                ProviderId = providerId,
                Version = 2,
                Status = ServiceScopeChangeRequestStatus.ApprovedByClient,
                Reason = "Aditivo anterior",
                AdditionalScopeDescription = "Escopo extra anterior",
                IncrementalValue = 50m,
                RequestedAtUtc = nowUtc.AddHours(-2)
            });

        ServiceScopeChangeRequest? created = null;
        _scopeChangeRequestRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ServiceScopeChangeRequest>()))
            .Callback<ServiceScopeChangeRequest>(item => created = item)
            .Returns(Task.CompletedTask);

        var result = await _service.CreateScopeChangeRequestAsync(
            providerId,
            UserRole.Provider.ToString(),
            appointmentId,
            new CreateServiceScopeChangeRequestDto(
                "Escopo aumentou durante a visita",
                "Necessario substituir cabos e disjuntores adicionais",
                199.90m));

        Assert.True(result.Success, $"{result.ErrorCode} - {result.ErrorMessage}");
        Assert.NotNull(result.ScopeChangeRequest);
        Assert.NotNull(created);
        Assert.Equal(3, created!.Version);
        Assert.Equal(ServiceScopeChangeRequestStatus.PendingClientApproval, created.Status);
        Assert.Equal("Escopo aumentou durante a visita", created.Reason);
        Assert.Equal("Necessario substituir cabos e disjuntores adicionais", created.AdditionalScopeDescription);
        Assert.Equal(199.90m, created.IncrementalValue);
        Assert.Equal(created.Id, result.ScopeChangeRequest!.Id);
        Assert.Equal(created.Version, result.ScopeChangeRequest.Version);
        Assert.Equal(previousVersionId, created.PreviousVersionId);

        _notificationServiceMock.Verify(n => n.SendNotificationAsync(
                clientId.ToString("N"),
                "Solicitacao de aditivo",
                It.Is<string>(m => m.Contains("R$", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<string>()),
            Times.Once);
        _appointmentRepositoryMock.Verify(
            r => r.AddHistoryAsync(It.Is<ServiceAppointmentHistory>(h =>
                h.ServiceAppointmentId == appointmentId &&
                h.Metadata != null &&
                h.Metadata.Contains("scope_change_audit") &&
                h.Metadata.Contains("created"))),
            Times.Once);
    }

    [Fact]
    public async Task CreateScopeChangeRequestAsync_ShouldReturnPolicyViolation_WhenValueExceedsPlanLimit()
    {
        var providerId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ProviderId = providerId,
                ClientId = Guid.NewGuid(),
                ServiceRequestId = requestId,
                Status = ServiceAppointmentStatus.InProgress
            });

        _requestRepositoryMock
            .Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(new ServiceRequest
            {
                Id = requestId,
                ClientId = Guid.NewGuid(),
                Category = ServiceCategory.Plumbing,
                Description = "Servico",
                AddressStreet = "Rua B",
                AddressCity = "Praia Grande",
                AddressZip = "11704150",
                Latitude = -24.01,
                Longitude = -46.41,
                Status = ServiceRequestStatus.InProgress
            });

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(providerId))
            .ReturnsAsync(new User
            {
                Id = providerId,
                Role = UserRole.Provider,
                IsActive = true,
                ProviderProfile = new ProviderProfile
                {
                    Plan = ProviderPlan.Trial
                }
            });

        var result = await _service.CreateScopeChangeRequestAsync(
            providerId,
            UserRole.Provider.ToString(),
            appointmentId,
            new CreateServiceScopeChangeRequestDto(
                "Escopo adicional extenso",
                "Troca completa da instalacao",
                250m));

        Assert.False(result.Success);
        Assert.Equal("policy_violation", result.ErrorCode);
        _scopeChangeRequestRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<ServiceScopeChangeRequest>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateScopeChangeRequestAsync_ShouldReturnPendingConflict_WhenPendingScopeChangeAlreadyExists()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var serviceRequest = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            Category = ServiceCategory.Plumbing,
            Status = ServiceRequestStatus.InProgress,
            Description = "Servico em execucao",
            AddressStreet = "Rua C",
            AddressCity = "Praia Grande",
            AddressZip = "11704150",
            Latitude = -24.01,
            Longitude = -46.41,
            CommercialVersion = 4,
            CommercialState = ServiceRequestCommercialState.PendingClientApproval,
            CommercialBaseValue = 500m,
            CommercialCurrentValue = 620m,
            Proposals =
            {
                new Proposal
                {
                    ProviderId = providerId,
                    Accepted = true,
                    IsInvalidated = false,
                    EstimatedValue = 500m
                }
            }
        };

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ProviderId = providerId,
                ClientId = clientId,
                ServiceRequestId = requestId,
                Status = ServiceAppointmentStatus.InProgress,
                ServiceRequest = serviceRequest
            });

        _requestRepositoryMock
            .Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(serviceRequest);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(providerId))
            .ReturnsAsync(new User
            {
                Id = providerId,
                Role = UserRole.Provider,
                IsActive = true,
                ProviderProfile = new ProviderProfile
                {
                    Plan = ProviderPlan.Bronze
                }
            });

        _scopeChangeRequestRepositoryMock
            .Setup(r => r.GetLatestByAppointmentIdAndStatusAsync(
                appointmentId,
                ServiceScopeChangeRequestStatus.PendingClientApproval))
            .ReturnsAsync(new ServiceScopeChangeRequest
            {
                Id = Guid.NewGuid(),
                ServiceAppointmentId = appointmentId,
                ServiceRequestId = requestId,
                ProviderId = providerId,
                Version = 3,
                Status = ServiceScopeChangeRequestStatus.PendingClientApproval,
                Reason = "Aguardando resposta anterior",
                AdditionalScopeDescription = "Ainda sem resposta do cliente",
                IncrementalValue = 120m,
                RequestedAtUtc = DateTime.UtcNow.AddMinutes(-5)
            });

        var result = await _service.CreateScopeChangeRequestAsync(
            providerId,
            UserRole.Provider.ToString(),
            appointmentId,
            new CreateServiceScopeChangeRequestDto(
                "Novo aditivo",
                "Escopo extra para outro item",
                90m));

        Assert.False(result.Success);
        Assert.Equal("scope_change_pending", result.ErrorCode);
        Assert.Equal(4, serviceRequest.CommercialVersion);
        Assert.Equal(ServiceRequestCommercialState.PendingClientApproval, serviceRequest.CommercialState);
        _scopeChangeRequestRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<ServiceScopeChangeRequest>()),
            Times.Never);
        _requestRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<ServiceRequest>()),
            Times.Never);
        _notificationServiceMock.Verify(
            n => n.SendNotificationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateScopeChangeRequestAsync_ShouldCreateNewRequest_WhenPendingScopeChangeHasTimedOut()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var timedOutScopeChangeId = Guid.NewGuid();
        var serviceRequest = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            Category = ServiceCategory.Plumbing,
            Status = ServiceRequestStatus.InProgress,
            Description = "Servico em execucao",
            AddressStreet = "Rua D",
            AddressCity = "Praia Grande",
            AddressZip = "11704150",
            Latitude = -24.01,
            Longitude = -46.41,
            CommercialVersion = 4,
            CommercialState = ServiceRequestCommercialState.PendingClientApproval,
            CommercialBaseValue = 500m,
            CommercialCurrentValue = 620m,
            Proposals =
            {
                new Proposal
                {
                    ProviderId = providerId,
                    Accepted = true,
                    IsInvalidated = false,
                    EstimatedValue = 500m
                }
            }
        };

        var timedOutPending = new ServiceScopeChangeRequest
        {
            Id = timedOutScopeChangeId,
            ServiceAppointmentId = appointmentId,
            ServiceRequestId = requestId,
            ProviderId = providerId,
            Version = 4,
            Status = ServiceScopeChangeRequestStatus.PendingClientApproval,
            Reason = "Aguardando resposta anterior",
            AdditionalScopeDescription = "Ainda sem resposta do cliente",
            IncrementalValue = 120m,
            RequestedAtUtc = DateTime.UtcNow.AddDays(-2)
        };

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ProviderId = providerId,
                ClientId = clientId,
                ServiceRequestId = requestId,
                Status = ServiceAppointmentStatus.InProgress,
                ServiceRequest = serviceRequest
            });

        _requestRepositoryMock
            .Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(serviceRequest);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(providerId))
            .ReturnsAsync(new User
            {
                Id = providerId,
                Role = UserRole.Provider,
                IsActive = true,
                ProviderProfile = new ProviderProfile
                {
                    Plan = ProviderPlan.Bronze
                }
            });

        _scopeChangeRequestRepositoryMock
            .Setup(r => r.GetLatestByAppointmentIdAndStatusAsync(
                appointmentId,
                ServiceScopeChangeRequestStatus.PendingClientApproval))
            .ReturnsAsync(timedOutPending);

        _scopeChangeRequestRepositoryMock
            .Setup(r => r.GetLatestByAppointmentIdAsync(appointmentId))
            .ReturnsAsync(timedOutPending);

        ServiceScopeChangeRequest? created = null;
        _scopeChangeRequestRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<ServiceScopeChangeRequest>()))
            .Callback<ServiceScopeChangeRequest>(item => created = item)
            .Returns(Task.CompletedTask);

        var result = await _service.CreateScopeChangeRequestAsync(
            providerId,
            UserRole.Provider.ToString(),
            appointmentId,
            new CreateServiceScopeChangeRequestDto(
                "Novo aditivo",
                "Escopo extra para outro item",
                90m));

        Assert.True(result.Success, $"{result.ErrorCode} - {result.ErrorMessage}");
        Assert.NotNull(created);
        Assert.Equal(5, created!.Version);
        Assert.Equal(timedOutScopeChangeId, created.PreviousVersionId);
        _scopeChangeRequestRepositoryMock.Verify(
            r => r.UpdateAsync(It.Is<ServiceScopeChangeRequest>(x =>
                x.Id == timedOutScopeChangeId &&
                x.Status == ServiceScopeChangeRequestStatus.Expired)),
            Times.Once);
        _scopeChangeRequestRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<ServiceScopeChangeRequest>()),
            Times.Once);
        _notificationServiceMock.Verify(
            n => n.SendNotificationAsync(
                providerId.ToString("N"),
                "Aditivo expirado",
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task AddScopeChangeAttachmentAsync_ShouldAttachEvidence_WhenScopeChangeIsPending()
    {
        var providerId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var scopeChangeId = Guid.NewGuid();

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ProviderId = providerId,
                ClientId = Guid.NewGuid(),
                ServiceRequestId = Guid.NewGuid(),
                Status = ServiceAppointmentStatus.InProgress
            });

        _scopeChangeRequestRepositoryMock
            .Setup(r => r.GetByIdWithAttachmentsAsync(scopeChangeId))
            .ReturnsAsync(new ServiceScopeChangeRequest
            {
                Id = scopeChangeId,
                ServiceAppointmentId = appointmentId,
                ProviderId = providerId,
                Status = ServiceScopeChangeRequestStatus.PendingClientApproval
            });

        ServiceScopeChangeRequestAttachment? savedAttachment = null;
        _scopeChangeRequestRepositoryMock
            .Setup(r => r.AddAttachmentAsync(It.IsAny<ServiceScopeChangeRequestAttachment>()))
            .Callback<ServiceScopeChangeRequestAttachment>(a => savedAttachment = a)
            .Returns(Task.CompletedTask);

        var result = await _service.AddScopeChangeAttachmentAsync(
            providerId,
            UserRole.Provider.ToString(),
            appointmentId,
            scopeChangeId,
            new RegisterServiceScopeChangeAttachmentDto(
                "/uploads/scope-changes/evidencia-1.jpg",
                "evidencia-1.jpg",
                "image/jpeg",
                1024));

        Assert.True(result.Success, $"{result.ErrorCode} - {result.ErrorMessage}");
        Assert.NotNull(result.Attachment);
        Assert.NotNull(savedAttachment);
        Assert.Equal("image", savedAttachment!.MediaKind);
        Assert.Equal(savedAttachment.FileUrl, result.Attachment!.FileUrl);
        _appointmentRepositoryMock.Verify(
            r => r.AddHistoryAsync(It.Is<ServiceAppointmentHistory>(h =>
                h.ServiceAppointmentId == appointmentId &&
                h.Metadata != null &&
                h.Metadata.Contains("scope_change_audit") &&
                h.Metadata.Contains("attachment_added"))),
            Times.Once);
    }

    [Fact]
    public async Task AddScopeChangeAttachmentAsync_ShouldReturnForbidden_WhenProviderIsNotOwner()
    {
        var providerId = Guid.NewGuid();
        var otherProviderId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var scopeChangeId = Guid.NewGuid();

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ProviderId = otherProviderId,
                ClientId = Guid.NewGuid(),
                ServiceRequestId = Guid.NewGuid(),
                Status = ServiceAppointmentStatus.InProgress
            });

        _scopeChangeRequestRepositoryMock
            .Setup(r => r.GetByIdWithAttachmentsAsync(scopeChangeId))
            .ReturnsAsync(new ServiceScopeChangeRequest
            {
                Id = scopeChangeId,
                ServiceAppointmentId = appointmentId,
                ProviderId = otherProviderId,
                Status = ServiceScopeChangeRequestStatus.PendingClientApproval
            });

        var result = await _service.AddScopeChangeAttachmentAsync(
            providerId,
            UserRole.Provider.ToString(),
            appointmentId,
            scopeChangeId,
            new RegisterServiceScopeChangeAttachmentDto(
                "/uploads/scope-changes/evidencia-2.jpg",
                "evidencia-2.jpg",
                "image/jpeg",
                2048));

        Assert.False(result.Success);
        Assert.Equal("forbidden", result.ErrorCode);
        _scopeChangeRequestRepositoryMock.Verify(
            r => r.AddAttachmentAsync(It.IsAny<ServiceScopeChangeRequestAttachment>()),
            Times.Never);
    }

    [Fact]
    public async Task AddScopeChangeAttachmentAsync_ShouldReturnInvalidState_WhenScopeChangeAlreadyResponded()
    {
        var providerId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var scopeChangeId = Guid.NewGuid();

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ProviderId = providerId,
                ClientId = Guid.NewGuid(),
                ServiceRequestId = Guid.NewGuid(),
                Status = ServiceAppointmentStatus.InProgress
            });

        _scopeChangeRequestRepositoryMock
            .Setup(r => r.GetByIdWithAttachmentsAsync(scopeChangeId))
            .ReturnsAsync(new ServiceScopeChangeRequest
            {
                Id = scopeChangeId,
                ServiceAppointmentId = appointmentId,
                ProviderId = providerId,
                Status = ServiceScopeChangeRequestStatus.ApprovedByClient
            });

        var result = await _service.AddScopeChangeAttachmentAsync(
            providerId,
            UserRole.Provider.ToString(),
            appointmentId,
            scopeChangeId,
            new RegisterServiceScopeChangeAttachmentDto(
                "/uploads/scope-changes/evidencia-3.jpg",
                "evidencia-3.jpg",
                "image/jpeg",
                1024));

        Assert.False(result.Success);
        Assert.Equal("invalid_state", result.ErrorCode);
        _scopeChangeRequestRepositoryMock.Verify(
            r => r.AddAttachmentAsync(It.IsAny<ServiceScopeChangeRequestAttachment>()),
            Times.Never);
    }

    [Fact]
    public async Task ApproveScopeChangeRequestAsync_ShouldApprovePendingRequest_WhenClientOwnsAppointment()
    {
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var scopeChangeId = Guid.NewGuid();
        var serviceRequest = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            CommercialVersion = 1,
            CommercialState = ServiceRequestCommercialState.PendingClientApproval,
            CommercialBaseValue = 350m,
            CommercialCurrentValue = 350m,
            Proposals =
            {
                new Proposal
                {
                    ProviderId = providerId,
                    Accepted = true,
                    IsInvalidated = false,
                    EstimatedValue = 350m
                }
            }
        };

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ClientId = clientId,
                ProviderId = providerId,
                ServiceRequestId = requestId
            });

        _requestRepositoryMock
            .Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(serviceRequest);

        _commercialValueServiceMock
            .Setup(s => s.RecalculateAsync(It.Is<ServiceRequest>(r => r.Id == requestId)))
            .ReturnsAsync(new ServiceRequestCommercialTotalsDto(350m, 120m, 470m));

        _scopeChangeRequestRepositoryMock
            .Setup(r => r.GetByIdWithAttachmentsAsync(scopeChangeId))
            .ReturnsAsync(new ServiceScopeChangeRequest
            {
                Id = scopeChangeId,
                ServiceAppointmentId = appointmentId,
                ServiceRequestId = requestId,
                ProviderId = providerId,
                Status = ServiceScopeChangeRequestStatus.PendingClientApproval,
                Version = 1,
                Reason = "Escopo extra",
                AdditionalScopeDescription = "Detalhes adicionais",
                IncrementalValue = 120m
            });

        var result = await _service.ApproveScopeChangeRequestAsync(
            clientId,
            UserRole.Client.ToString(),
            appointmentId,
            scopeChangeId);

        Assert.True(result.Success, $"{result.ErrorCode} - {result.ErrorMessage}");
        Assert.NotNull(result.ScopeChangeRequest);
        Assert.Equal(ServiceScopeChangeRequestStatus.ApprovedByClient.ToString(), result.ScopeChangeRequest!.Status);
        _scopeChangeRequestRepositoryMock.Verify(
            r => r.UpdateAsync(It.Is<ServiceScopeChangeRequest>(x =>
                x.Id == scopeChangeId &&
                x.Status == ServiceScopeChangeRequestStatus.ApprovedByClient)),
            Times.Once);
        _requestRepositoryMock.Verify(
            r => r.UpdateAsync(It.Is<ServiceRequest>(sr =>
                sr.Id == requestId &&
                sr.CommercialState == ServiceRequestCommercialState.Stable &&
                sr.CommercialVersion == 2 &&
                sr.CommercialCurrentValue == 470m)),
            Times.Once);
        _appointmentRepositoryMock.Verify(
            r => r.AddHistoryAsync(It.Is<ServiceAppointmentHistory>(h =>
                h.ServiceAppointmentId == appointmentId &&
                h.Metadata != null &&
                h.Metadata.Contains("scope_change_audit") &&
                h.Metadata.Contains("approved"))),
            Times.Once);
        _notificationServiceMock.Verify(
            n => n.SendNotificationAsync(
                providerId.ToString("N"),
                "Aditivo aprovado pelo cliente",
                It.Is<string>(m =>
                    m.Contains("Aditivo v1 aprovado", StringComparison.OrdinalIgnoreCase) &&
                    m.Contains("Valor anterior", StringComparison.OrdinalIgnoreCase) &&
                    m.Contains("Novo valor", StringComparison.OrdinalIgnoreCase)),
                It.Is<string>(url => url.Contains("scopeChangeId", StringComparison.OrdinalIgnoreCase))),
            Times.Once);
        _notificationServiceMock.Verify(
            n => n.SendNotificationAsync(
                clientId.ToString("N"),
                "Aditivo aprovado",
                It.Is<string>(m =>
                    m.Contains("Voce aprovou o aditivo", StringComparison.OrdinalIgnoreCase) &&
                    m.Contains("Aditivo v1 aprovado", StringComparison.OrdinalIgnoreCase)),
                It.Is<string>(url => url.Contains("scopeChangeId", StringComparison.OrdinalIgnoreCase))),
            Times.Once);
    }

    [Fact]
    public async Task ApproveScopeChangeRequestAsync_ShouldReturnScopeChangeExpired_WhenPendingRequestHasTimedOut()
    {
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var scopeChangeId = Guid.NewGuid();
        var serviceRequest = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            CommercialVersion = 3,
            CommercialState = ServiceRequestCommercialState.PendingClientApproval,
            CommercialBaseValue = 350m,
            CommercialCurrentValue = 350m,
            Proposals =
            {
                new Proposal
                {
                    ProviderId = providerId,
                    Accepted = true,
                    IsInvalidated = false,
                    EstimatedValue = 350m
                }
            }
        };

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ClientId = clientId,
                ProviderId = providerId,
                ServiceRequestId = requestId,
                ServiceRequest = serviceRequest
            });

        _requestRepositoryMock
            .Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(serviceRequest);

        _scopeChangeRequestRepositoryMock
            .Setup(r => r.GetByIdWithAttachmentsAsync(scopeChangeId))
            .ReturnsAsync(new ServiceScopeChangeRequest
            {
                Id = scopeChangeId,
                ServiceAppointmentId = appointmentId,
                ServiceRequestId = requestId,
                ProviderId = providerId,
                Status = ServiceScopeChangeRequestStatus.PendingClientApproval,
                Version = 2,
                Reason = "Escopo extra",
                AdditionalScopeDescription = "Trocar peca adicional",
                IncrementalValue = 90m,
                RequestedAtUtc = DateTime.UtcNow.AddDays(-2)
            });

        _commercialValueServiceMock
            .Setup(s => s.RecalculateAsync(It.Is<ServiceRequest>(r => r.Id == requestId)))
            .ReturnsAsync(new ServiceRequestCommercialTotalsDto(350m, 0m, 350m));

        var result = await _service.ApproveScopeChangeRequestAsync(
            clientId,
            UserRole.Client.ToString(),
            appointmentId,
            scopeChangeId);

        Assert.False(result.Success);
        Assert.Equal("scope_change_expired", result.ErrorCode);
        _scopeChangeRequestRepositoryMock.Verify(
            r => r.UpdateAsync(It.Is<ServiceScopeChangeRequest>(x =>
                x.Id == scopeChangeId &&
                x.Status == ServiceScopeChangeRequestStatus.Expired)),
            Times.Once);
        _requestRepositoryMock.Verify(
            r => r.UpdateAsync(It.Is<ServiceRequest>(sr =>
                sr.Id == requestId &&
                sr.CommercialState == ServiceRequestCommercialState.Stable &&
                sr.CommercialVersion == 3 &&
                sr.CommercialCurrentValue == 350m)),
            Times.Once);
        _notificationServiceMock.Verify(
            n => n.SendNotificationAsync(
                providerId.ToString("N"),
                "Aditivo expirado",
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Once);
        _notificationServiceMock.Verify(
            n => n.SendNotificationAsync(
                clientId.ToString("N"),
                "Aditivo expirado",
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Once);
        _notificationServiceMock.Verify(
            n => n.SendNotificationAsync(
                providerId.ToString("N"),
                "Aditivo aprovado pelo cliente",
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ApproveScopeChangeRequestAsync_ShouldBeIdempotent_WhenClientRepeatsSameApproval()
    {
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var scopeChangeId = Guid.NewGuid();
        var serviceRequest = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            CommercialVersion = 1,
            CommercialState = ServiceRequestCommercialState.PendingClientApproval,
            CommercialBaseValue = 300m,
            CommercialCurrentValue = 300m,
            Proposals =
            {
                new Proposal
                {
                    ProviderId = providerId,
                    Accepted = true,
                    IsInvalidated = false,
                    EstimatedValue = 300m
                }
            }
        };

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ClientId = clientId,
                ProviderId = providerId,
                ServiceRequestId = requestId
            });

        _requestRepositoryMock
            .Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(serviceRequest);

        _commercialValueServiceMock
            .Setup(s => s.RecalculateAsync(It.Is<ServiceRequest>(r => r.Id == requestId)))
            .ReturnsAsync(new ServiceRequestCommercialTotalsDto(300m, 80m, 380m));

        var scopeChange = new ServiceScopeChangeRequest
        {
            Id = scopeChangeId,
            ServiceAppointmentId = appointmentId,
            ServiceRequestId = requestId,
            ProviderId = providerId,
            Status = ServiceScopeChangeRequestStatus.PendingClientApproval,
            Version = 1,
            Reason = "Escopo extra",
            AdditionalScopeDescription = "Incluir item adicional",
            IncrementalValue = 80m
        };

        _scopeChangeRequestRepositoryMock
            .Setup(r => r.GetByIdWithAttachmentsAsync(scopeChangeId))
            .ReturnsAsync(scopeChange);

        var firstAttempt = await _service.ApproveScopeChangeRequestAsync(
            clientId,
            UserRole.Client.ToString(),
            appointmentId,
            scopeChangeId);

        var secondAttempt = await _service.ApproveScopeChangeRequestAsync(
            clientId,
            UserRole.Client.ToString(),
            appointmentId,
            scopeChangeId);

        Assert.True(firstAttempt.Success, $"{firstAttempt.ErrorCode} - {firstAttempt.ErrorMessage}");
        Assert.False(secondAttempt.Success);
        Assert.Equal("invalid_state", secondAttempt.ErrorCode);
        Assert.Equal(2, serviceRequest.CommercialVersion);
        Assert.Equal(ServiceRequestCommercialState.Stable, serviceRequest.CommercialState);
        Assert.Equal(380m, serviceRequest.CommercialCurrentValue);
        _scopeChangeRequestRepositoryMock.Verify(
            r => r.UpdateAsync(It.Is<ServiceScopeChangeRequest>(x =>
                x.Id == scopeChangeId &&
                x.Status == ServiceScopeChangeRequestStatus.ApprovedByClient)),
            Times.Once);
        _requestRepositoryMock.Verify(
            r => r.UpdateAsync(It.Is<ServiceRequest>(sr =>
                sr.Id == requestId &&
                sr.CommercialVersion == 2 &&
                sr.CommercialCurrentValue == 380m)),
            Times.Once);
        _notificationServiceMock.Verify(
            n => n.SendNotificationAsync(
                providerId.ToString("N"),
                "Aditivo aprovado pelo cliente",
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ApproveScopeChangeRequestAsync_ShouldReturnForbidden_WhenClientDoesNotOwnAppointment()
    {
        var actorClientId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ClientId = Guid.NewGuid(),
                ProviderId = Guid.NewGuid(),
                ServiceRequestId = Guid.NewGuid()
            });

        var result = await _service.ApproveScopeChangeRequestAsync(
            actorClientId,
            UserRole.Client.ToString(),
            appointmentId,
            Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Equal("forbidden", result.ErrorCode);
        _scopeChangeRequestRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<ServiceScopeChangeRequest>()),
            Times.Never);
    }

    [Fact]
    public async Task RejectScopeChangeRequestAsync_ShouldReturnInvalidReason_WhenReasonIsMissing()
    {
        var result = await _service.RejectScopeChangeRequestAsync(
            Guid.NewGuid(),
            UserRole.Client.ToString(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new RejectServiceScopeChangeRequestDto(string.Empty));

        Assert.False(result.Success);
        Assert.Equal("invalid_reason", result.ErrorCode);
    }

    [Fact]
    public async Task RejectScopeChangeRequestAsync_ShouldRejectPendingRequest_AndAppendAuditTrail()
    {
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var scopeChangeId = Guid.NewGuid();
        var serviceRequest = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            CommercialVersion = 2,
            CommercialState = ServiceRequestCommercialState.PendingClientApproval,
            CommercialBaseValue = 300m,
            CommercialCurrentValue = 480m,
            Proposals =
            {
                new Proposal
                {
                    ProviderId = providerId,
                    Accepted = true,
                    IsInvalidated = false,
                    EstimatedValue = 300m
                }
            }
        };

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ClientId = clientId,
                ProviderId = providerId,
                ServiceRequestId = requestId
            });

        _requestRepositoryMock
            .Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(serviceRequest);

        _scopeChangeRequestRepositoryMock
            .Setup(r => r.GetByIdWithAttachmentsAsync(scopeChangeId))
            .ReturnsAsync(new ServiceScopeChangeRequest
            {
                Id = scopeChangeId,
                ServiceAppointmentId = appointmentId,
                ServiceRequestId = requestId,
                ProviderId = providerId,
                Status = ServiceScopeChangeRequestStatus.PendingClientApproval,
                Version = 3,
                Reason = "Escopo maior",
                AdditionalScopeDescription = "Adicionar instalacao extra",
                IncrementalValue = 180m
            });

        var result = await _service.RejectScopeChangeRequestAsync(
            clientId,
            UserRole.Client.ToString(),
            appointmentId,
            scopeChangeId,
            new RejectServiceScopeChangeRequestDto("Nao concordo com o valor"));

        Assert.True(result.Success, $"{result.ErrorCode} - {result.ErrorMessage}");
        Assert.NotNull(result.ScopeChangeRequest);
        Assert.Equal(ServiceScopeChangeRequestStatus.RejectedByClient.ToString(), result.ScopeChangeRequest!.Status);
        _scopeChangeRequestRepositoryMock.Verify(
            r => r.UpdateAsync(It.Is<ServiceScopeChangeRequest>(x =>
                x.Id == scopeChangeId &&
                x.Status == ServiceScopeChangeRequestStatus.RejectedByClient &&
                x.ClientResponseReason == "Nao concordo com o valor")),
            Times.Once);
        _requestRepositoryMock.Verify(
            r => r.UpdateAsync(It.Is<ServiceRequest>(sr =>
                sr.Id == requestId &&
                sr.CommercialState == ServiceRequestCommercialState.Stable &&
                sr.CommercialVersion == 2 &&
                sr.CommercialCurrentValue == 480m)),
            Times.Once);
        _appointmentRepositoryMock.Verify(
            r => r.AddHistoryAsync(It.Is<ServiceAppointmentHistory>(h =>
                h.ServiceAppointmentId == appointmentId &&
                h.Metadata != null &&
                h.Metadata.Contains("scope_change_audit") &&
                h.Metadata.Contains("rejected"))),
            Times.Once);
        _notificationServiceMock.Verify(
            n => n.SendNotificationAsync(
                providerId.ToString("N"),
                "Aditivo rejeitado pelo cliente",
                It.Is<string>(m =>
                    m.Contains("Aditivo v3 rejeitado", StringComparison.OrdinalIgnoreCase) &&
                    m.Contains("Motivo informado", StringComparison.OrdinalIgnoreCase) &&
                    m.Contains("Nao concordo com o valor", StringComparison.OrdinalIgnoreCase)),
                It.Is<string>(url => url.Contains("scopeChangeId", StringComparison.OrdinalIgnoreCase))),
            Times.Once);
        _notificationServiceMock.Verify(
            n => n.SendNotificationAsync(
                clientId.ToString("N"),
                "Aditivo rejeitado",
                It.Is<string>(m =>
                    m.Contains("Voce rejeitou o aditivo", StringComparison.OrdinalIgnoreCase) &&
                    m.Contains("Aditivo v3 rejeitado", StringComparison.OrdinalIgnoreCase)),
                It.Is<string>(url => url.Contains("scopeChangeId", StringComparison.OrdinalIgnoreCase))),
            Times.Once);
    }

    [Fact]
    public async Task RejectScopeChangeRequestAsync_ShouldReturnInvalidState_WhenScopeChangeAlreadyAnswered()
    {
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var scopeChangeId = Guid.NewGuid();

        _appointmentRepositoryMock
            .Setup(r => r.GetByIdAsync(appointmentId))
            .ReturnsAsync(new ServiceAppointment
            {
                Id = appointmentId,
                ClientId = clientId,
                ProviderId = providerId,
                ServiceRequestId = requestId
            });

        _scopeChangeRequestRepositoryMock
            .Setup(r => r.GetByIdWithAttachmentsAsync(scopeChangeId))
            .ReturnsAsync(new ServiceScopeChangeRequest
            {
                Id = scopeChangeId,
                ServiceAppointmentId = appointmentId,
                ServiceRequestId = requestId,
                ProviderId = providerId,
                Status = ServiceScopeChangeRequestStatus.ApprovedByClient,
                Version = 2,
                Reason = "Escopo extra",
                AdditionalScopeDescription = "Detalhes",
                IncrementalValue = 110m
            });

        var result = await _service.RejectScopeChangeRequestAsync(
            clientId,
            UserRole.Client.ToString(),
            appointmentId,
            scopeChangeId,
            new RejectServiceScopeChangeRequestDto("Nao concordo"));

        Assert.False(result.Success);
        Assert.Equal("invalid_state", result.ErrorCode);
        _scopeChangeRequestRepositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<ServiceScopeChangeRequest>()),
            Times.Never);
    }

    [Fact]
    public async Task GetScopeChangeRequestsByServiceRequestAsync_ShouldReturnScopeChanges_WhenClientOwnsAppointments()
    {
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var providerA = Guid.NewGuid();
        var providerB = Guid.NewGuid();
        var appointmentA = Guid.NewGuid();
        var appointmentB = Guid.NewGuid();

        _appointmentRepositoryMock
            .Setup(r => r.GetByRequestIdAsync(requestId))
            .ReturnsAsync(new List<ServiceAppointment>
            {
                new()
                {
                    Id = appointmentA,
                    ServiceRequestId = requestId,
                    ClientId = clientId,
                    ProviderId = providerA,
                    Status = ServiceAppointmentStatus.InProgress
                },
                new()
                {
                    Id = appointmentB,
                    ServiceRequestId = requestId,
                    ClientId = clientId,
                    ProviderId = providerB,
                    Status = ServiceAppointmentStatus.Confirmed
                }
            });

        _scopeChangeRequestRepositoryMock
            .Setup(r => r.GetByServiceRequestIdAsync(requestId))
            .ReturnsAsync(new List<ServiceScopeChangeRequest>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ServiceRequestId = requestId,
                    ServiceAppointmentId = appointmentB,
                    ProviderId = providerB,
                    Version = 2,
                    Status = ServiceScopeChangeRequestStatus.ApprovedByClient,
                    Reason = "Aditivo 2",
                    AdditionalScopeDescription = "Escopo 2",
                    IncrementalValue = 150m,
                    RequestedAtUtc = DateTime.UtcNow.AddMinutes(-10)
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ServiceRequestId = requestId,
                    ServiceAppointmentId = appointmentA,
                    ProviderId = providerA,
                    Version = 1,
                    Status = ServiceScopeChangeRequestStatus.PendingClientApproval,
                    Reason = "Aditivo 1",
                    AdditionalScopeDescription = "Escopo 1",
                    IncrementalValue = 80m,
                    RequestedAtUtc = DateTime.UtcNow.AddMinutes(-20)
                }
            });

        var result = await _service.GetScopeChangeRequestsByServiceRequestAsync(
            clientId,
            UserRole.Client.ToString(),
            requestId);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, item => item.ServiceAppointmentId == appointmentA);
        Assert.Contains(result, item => item.ServiceAppointmentId == appointmentB);
        Assert.True(result[0].RequestedAtUtc >= result[1].RequestedAtUtc);
    }

    [Fact]
    public async Task GetScopeChangeRequestsByServiceRequestAsync_ShouldFilterScopeChanges_WhenProviderAccessesRequest()
    {
        var requestId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var otherProviderId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var providerAppointmentId = Guid.NewGuid();
        var otherAppointmentId = Guid.NewGuid();

        _appointmentRepositoryMock
            .Setup(r => r.GetByRequestIdAsync(requestId))
            .ReturnsAsync(new List<ServiceAppointment>
            {
                new()
                {
                    Id = providerAppointmentId,
                    ServiceRequestId = requestId,
                    ClientId = clientId,
                    ProviderId = providerId,
                    Status = ServiceAppointmentStatus.InProgress
                },
                new()
                {
                    Id = otherAppointmentId,
                    ServiceRequestId = requestId,
                    ClientId = clientId,
                    ProviderId = otherProviderId,
                    Status = ServiceAppointmentStatus.InProgress
                }
            });

        _scopeChangeRequestRepositoryMock
            .Setup(r => r.GetByServiceRequestIdAsync(requestId))
            .ReturnsAsync(new List<ServiceScopeChangeRequest>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ServiceRequestId = requestId,
                    ServiceAppointmentId = providerAppointmentId,
                    ProviderId = providerId,
                    Version = 1,
                    Status = ServiceScopeChangeRequestStatus.PendingClientApproval,
                    Reason = "Meu aditivo",
                    AdditionalScopeDescription = "Escopo provider",
                    IncrementalValue = 70m,
                    RequestedAtUtc = DateTime.UtcNow.AddMinutes(-5)
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ServiceRequestId = requestId,
                    ServiceAppointmentId = otherAppointmentId,
                    ProviderId = otherProviderId,
                    Version = 1,
                    Status = ServiceScopeChangeRequestStatus.PendingClientApproval,
                    Reason = "Aditivo de outro prestador",
                    AdditionalScopeDescription = "Escopo outro",
                    IncrementalValue = 90m,
                    RequestedAtUtc = DateTime.UtcNow.AddMinutes(-1)
                }
            });

        var result = await _service.GetScopeChangeRequestsByServiceRequestAsync(
            providerId,
            UserRole.Provider.ToString(),
            requestId);

        Assert.Single(result);
        Assert.Equal(providerAppointmentId, result[0].ServiceAppointmentId);
        Assert.Equal(providerId, result[0].ProviderId);
    }

    [Fact]
    public async Task GetScopeChangeRequestsByServiceRequestAsync_ShouldReturnEmpty_WhenActorRoleIsUnknown()
    {
        var result = await _service.GetScopeChangeRequestsByServiceRequestAsync(
            Guid.NewGuid(),
            "Guest",
            Guid.NewGuid());

        Assert.Empty(result);
        _appointmentRepositoryMock.Verify(
            r => r.GetByRequestIdAsync(It.IsAny<Guid>()),
            Times.Never);
    }

    private static ServiceRequest BuildRequest(Guid clientId, Guid providerId, bool acceptedProposal)
    {
        return new ServiceRequest
        {
            ClientId = clientId,
            Category = ServiceCategory.Electrical,
            Description = "Troca de disjuntor",
            AddressStreet = "Rua A",
            AddressCity = "Praia Grande",
            AddressZip = "11704150",
            Latitude = -24.01,
            Longitude = -46.41,
            Status = ServiceRequestStatus.Scheduled,
            Proposals =
            {
                new Proposal
                {
                    ProviderId = providerId,
                    Accepted = acceptedProposal,
                    IsInvalidated = false
                }
            }
        };
    }

    private static DateTime NextUtcDayOfWeek(DateTime fromUtc, DayOfWeek dayOfWeek)
    {
        var date = fromUtc.Date;
        var offset = ((int)dayOfWeek - (int)date.DayOfWeek + 7) % 7;
        if (offset == 0)
        {
            offset = 7;
        }

        return DateTime.SpecifyKind(date.AddDays(offset), DateTimeKind.Utc);
    }
}
