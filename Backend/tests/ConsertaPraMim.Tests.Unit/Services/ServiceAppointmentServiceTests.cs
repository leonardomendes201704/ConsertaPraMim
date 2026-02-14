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
    private readonly ServiceAppointmentService _service;

    public ServiceAppointmentServiceTests()
    {
        _appointmentRepositoryMock = new Mock<IServiceAppointmentRepository>();
        _requestRepositoryMock = new Mock<IServiceRequestRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _notificationServiceMock = new Mock<INotificationService>();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceAppointments:ConfirmationExpiryHours"] = "12",
                ["ServiceAppointments:CancelMinimumHoursBeforeWindow"] = "2",
                ["ServiceAppointments:RescheduleMinimumHoursBeforeWindow"] = "2",
                ["ServiceAppointments:RescheduleMaximumAdvanceDays"] = "30",
                ["ServiceAppointments:AvailabilityTimeZoneId"] = "UTC"
            })
            .Build();

        _service = new ServiceAppointmentService(
            _appointmentRepositoryMock.Object,
            _requestRepositoryMock.Object,
            _userRepositoryMock.Object,
            _notificationServiceMock.Object,
            configuration);
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

        var dto = new CreateServiceAppointmentRequestDto(
            requestId,
            providerId,
            DateTime.UtcNow.AddHours(2),
            DateTime.UtcNow.AddHours(3));

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
