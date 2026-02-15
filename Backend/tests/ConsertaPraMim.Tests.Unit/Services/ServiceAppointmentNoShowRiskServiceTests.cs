using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class ServiceAppointmentNoShowRiskServiceTests
{
    private readonly Mock<IServiceAppointmentRepository> _appointmentRepositoryMock = new();
    private readonly Mock<IServiceAppointmentNoShowRiskPolicyRepository> _policyRepositoryMock = new();
    private readonly Mock<IServiceAppointmentNoShowQueueRepository> _queueRepositoryMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly ServiceAppointmentNoShowRiskService _service;

    public ServiceAppointmentNoShowRiskServiceTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceAppointments:NoShowRisk:LookaheadHours"] = "24",
                ["ServiceAppointments:NoShowRisk:IncludePastWindowMinutes"] = "30"
            })
            .Build();

        _service = new ServiceAppointmentNoShowRiskService(
            _appointmentRepositoryMock.Object,
            _policyRepositoryMock.Object,
            _queueRepositoryMock.Object,
            _notificationServiceMock.Object,
            configuration,
            NullLogger<ServiceAppointmentNoShowRiskService>.Instance);
    }

    [Fact]
    public async Task EvaluateNoShowRiskAsync_ShouldSetHighQueueAndNotify_WhenSignalsAreCritical()
    {
        var appointment = BuildAppointment(windowStartUtc: DateTime.UtcNow.AddHours(1));
        var policy = BuildPolicy();

        _policyRepositoryMock
            .Setup(r => r.GetActiveAsync())
            .ReturnsAsync(policy);

        _appointmentRepositoryMock
            .Setup(r => r.GetNoShowRiskCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>()))
            .ReturnsAsync(new List<ServiceAppointment> { appointment });

        _appointmentRepositoryMock
            .Setup(r => r.CountClientNoShowRiskEventsAsync(appointment.ClientId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(3);

        _appointmentRepositoryMock
            .Setup(r => r.CountProviderNoShowRiskEventsAsync(appointment.ProviderId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(2);

        _queueRepositoryMock
            .Setup(r => r.GetByAppointmentIdAsync(appointment.Id))
            .ReturnsAsync((ServiceAppointmentNoShowQueueItem?)null);

        var processed = await _service.EvaluateNoShowRiskAsync();

        Assert.Equal(1, processed);
        Assert.Equal(100, appointment.NoShowRiskScore);
        Assert.Equal(ServiceAppointmentNoShowRiskLevel.High, appointment.NoShowRiskLevel);
        Assert.Contains("both_presence_not_confirmed", appointment.NoShowRiskReasons ?? string.Empty, StringComparison.Ordinal);

        _appointmentRepositoryMock.Verify(r => r.UpdateAsync(appointment), Times.Once);
        _appointmentRepositoryMock.Verify(r => r.AddHistoryAsync(It.IsAny<ServiceAppointmentHistory>()), Times.Once);
        _queueRepositoryMock.Verify(r => r.AddAsync(It.Is<ServiceAppointmentNoShowQueueItem>(q =>
            q.ServiceAppointmentId == appointment.Id &&
            q.Score == 100 &&
            q.RiskLevel == ServiceAppointmentNoShowRiskLevel.High &&
            q.Status == ServiceAppointmentNoShowQueueStatus.Open)), Times.Once);
        _notificationServiceMock.Verify(r => r.SendNotificationAsync(
            It.IsAny<string>(),
            It.Is<string>(s => s.Contains("risco alto", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Exactly(2));
    }

    [Fact]
    public async Task EvaluateNoShowRiskAsync_ShouldResolveQueue_WhenRiskDropsToLow()
    {
        var appointment = BuildAppointment(
            windowStartUtc: DateTime.UtcNow.AddHours(30),
            clientPresenceConfirmed: true,
            providerPresenceConfirmed: true);
        appointment.NoShowRiskScore = 85;
        appointment.NoShowRiskLevel = ServiceAppointmentNoShowRiskLevel.High;
        appointment.NoShowRiskReasons = "both_presence_not_confirmed,window_within_2h";

        var existingQueue = new ServiceAppointmentNoShowQueueItem
        {
            ServiceAppointmentId = appointment.Id,
            RiskLevel = ServiceAppointmentNoShowRiskLevel.High,
            Score = 85,
            ReasonsCsv = appointment.NoShowRiskReasons,
            Status = ServiceAppointmentNoShowQueueStatus.Open,
            FirstDetectedAtUtc = DateTime.UtcNow.AddHours(-2),
            LastDetectedAtUtc = DateTime.UtcNow.AddHours(-1)
        };

        _policyRepositoryMock
            .Setup(r => r.GetActiveAsync())
            .ReturnsAsync(BuildPolicy());

        _appointmentRepositoryMock
            .Setup(r => r.GetNoShowRiskCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>()))
            .ReturnsAsync(new List<ServiceAppointment> { appointment });

        _appointmentRepositoryMock
            .Setup(r => r.CountClientNoShowRiskEventsAsync(appointment.ClientId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        _appointmentRepositoryMock
            .Setup(r => r.CountProviderNoShowRiskEventsAsync(appointment.ProviderId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        _queueRepositoryMock
            .Setup(r => r.GetByAppointmentIdAsync(appointment.Id))
            .ReturnsAsync(existingQueue);

        var processed = await _service.EvaluateNoShowRiskAsync();

        Assert.Equal(1, processed);
        Assert.Equal(0, appointment.NoShowRiskScore);
        Assert.Equal(ServiceAppointmentNoShowRiskLevel.Low, appointment.NoShowRiskLevel);

        _queueRepositoryMock.Verify(r => r.UpdateAsync(It.Is<ServiceAppointmentNoShowQueueItem>(q =>
            q.ServiceAppointmentId == appointment.Id &&
            q.Status == ServiceAppointmentNoShowQueueStatus.Resolved &&
            q.ResolutionNote != null &&
            q.ResolutionNote.Contains("normalizado", StringComparison.OrdinalIgnoreCase))), Times.Once);
        _notificationServiceMock.Verify(r => r.SendNotificationAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateNoShowRiskAsync_ShouldNotWriteHistory_WhenAssessmentUnchanged()
    {
        var appointment = BuildAppointment(
            windowStartUtc: DateTime.UtcNow.AddHours(4),
            clientPresenceConfirmed: null,
            providerPresenceConfirmed: true);
        appointment.NoShowRiskScore = 40;
        appointment.NoShowRiskLevel = ServiceAppointmentNoShowRiskLevel.Medium;
        appointment.NoShowRiskReasons = "client_presence_not_confirmed,window_within_6h";

        _policyRepositoryMock
            .Setup(r => r.GetActiveAsync())
            .ReturnsAsync(BuildPolicy());

        _appointmentRepositoryMock
            .Setup(r => r.GetNoShowRiskCandidatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>()))
            .ReturnsAsync(new List<ServiceAppointment> { appointment });

        _appointmentRepositoryMock
            .Setup(r => r.CountClientNoShowRiskEventsAsync(appointment.ClientId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        _appointmentRepositoryMock
            .Setup(r => r.CountProviderNoShowRiskEventsAsync(appointment.ProviderId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        _queueRepositoryMock
            .Setup(r => r.GetByAppointmentIdAsync(appointment.Id))
            .ReturnsAsync((ServiceAppointmentNoShowQueueItem?)null);

        var processed = await _service.EvaluateNoShowRiskAsync();

        Assert.Equal(1, processed);
        Assert.Equal(40, appointment.NoShowRiskScore);
        Assert.Equal(ServiceAppointmentNoShowRiskLevel.Medium, appointment.NoShowRiskLevel);
        Assert.Equal("client_presence_not_confirmed,window_within_6h", appointment.NoShowRiskReasons);

        _appointmentRepositoryMock.Verify(r => r.AddHistoryAsync(It.IsAny<ServiceAppointmentHistory>()), Times.Never);
        _notificationServiceMock.Verify(r => r.SendNotificationAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    private static ServiceAppointmentNoShowRiskPolicy BuildPolicy()
    {
        return new ServiceAppointmentNoShowRiskPolicy
        {
            IsActive = true,
            LookbackDays = 90,
            MaxHistoryEventsPerActor = 20,
            MinClientHistoryRiskEvents = 2,
            MinProviderHistoryRiskEvents = 2,
            WeightClientNotConfirmed = 25,
            WeightProviderNotConfirmed = 25,
            WeightBothNotConfirmedBonus = 10,
            WeightWindowWithin24Hours = 10,
            WeightWindowWithin6Hours = 15,
            WeightWindowWithin2Hours = 20,
            WeightClientHistoryRisk = 10,
            WeightProviderHistoryRisk = 10,
            LowThresholdScore = 0,
            MediumThresholdScore = 40,
            HighThresholdScore = 70
        };
    }

    private static ServiceAppointment BuildAppointment(
        DateTime windowStartUtc,
        bool? clientPresenceConfirmed = null,
        bool? providerPresenceConfirmed = null)
    {
        var requestId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        return new ServiceAppointment
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            ClientId = clientId,
            ProviderId = providerId,
            Status = ServiceAppointmentStatus.Confirmed,
            WindowStartUtc = windowStartUtc,
            WindowEndUtc = windowStartUtc.AddHours(1),
            ClientPresenceConfirmed = clientPresenceConfirmed,
            ProviderPresenceConfirmed = providerPresenceConfirmed
        };
    }
}
