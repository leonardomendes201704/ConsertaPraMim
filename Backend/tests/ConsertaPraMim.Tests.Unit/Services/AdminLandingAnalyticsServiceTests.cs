using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminLandingAnalyticsServiceTests
{
    [Fact(DisplayName = "Admin landing analytics service | Overview | Deve consolidar sessoes, heatmap e leads")]
    public async Task GetOverviewAsync_ShouldAggregateSessionsAndHeatmap()
    {
        var accessRepositoryMock = new Mock<ILandingAccessEventRepository>();
        var telemetryRepositoryMock = new Mock<ILandingTelemetryEventRepository>();
        var leadRepositoryMock = new Mock<ILandingLeadRepository>();
        var runtimeSettingsMock = new Mock<ILandingAnalyticsRuntimeSettings>();

        var fromUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc);
        var clientSessionId = "session-client-001";
        var providerSessionId = "session-provider-001";
        var leadId = Guid.NewGuid();

        accessRepositoryMock
            .Setup(repository => repository.GetByPeriodAsync(fromUtc, toUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new LandingAccessEvent
                {
                    SessionId = clientSessionId,
                    VisitorId = "visitor-client-001",
                    CreatedAt = fromUtc.AddHours(10),
                    Path = "/Cliente",
                    CurrentUrl = "https://www.consertapramim.com/Cliente",
                    InitialLeadOrigin = LandingLeadOrigin.Client,
                    GeoCountry = "Brasil",
                    GeoCountryCode = "BR",
                    GeoRegion = "Sao Paulo",
                    GeoRegionCode = "SP",
                    GeoCity = "Praia Grande"
                },
                new LandingAccessEvent
                {
                    SessionId = providerSessionId,
                    VisitorId = "visitor-provider-001",
                    CreatedAt = fromUtc.AddHours(11),
                    Path = "/Prestador",
                    CurrentUrl = "https://www.consertapramim.com/Prestador",
                    InitialLeadOrigin = LandingLeadOrigin.Provider,
                    GeoCountry = "Brasil",
                    GeoCountryCode = "BR",
                    GeoRegion = "Sao Paulo",
                    GeoRegionCode = "SP",
                    GeoCity = "Santos"
                }
            ]);

        telemetryRepositoryMock
            .Setup(repository => repository.GetByPeriodAsync(fromUtc, toUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new LandingTelemetryEvent
                {
                    SessionId = clientSessionId,
                    VisitorId = "visitor-client-001",
                    OccurredAtUtc = fromUtc.AddHours(10).AddMinutes(1),
                    EventType = LandingTelemetryEventType.Heartbeat,
                    ActiveSeconds = 15
                },
                new LandingTelemetryEvent
                {
                    SessionId = clientSessionId,
                    VisitorId = "visitor-client-001",
                    OccurredAtUtc = fromUtc.AddHours(10).AddMinutes(2),
                    EventType = LandingTelemetryEventType.ScrollMilestone,
                    ScrollDepthPercent = 75
                },
                new LandingTelemetryEvent
                {
                    SessionId = clientSessionId,
                    VisitorId = "visitor-client-001",
                    OccurredAtUtc = fromUtc.AddHours(10).AddMinutes(3),
                    EventType = LandingTelemetryEventType.Click,
                    HeatmapRow = 1,
                    HeatmapColumn = 2,
                    ElementLabel = "Encontrar profissional"
                },
                new LandingTelemetryEvent
                {
                    SessionId = clientSessionId,
                    VisitorId = "visitor-client-001",
                    OccurredAtUtc = fromUtc.AddHours(10).AddMinutes(4),
                    EventType = LandingTelemetryEventType.LeadModalOpen
                },
                new LandingTelemetryEvent
                {
                    SessionId = clientSessionId,
                    VisitorId = "visitor-client-001",
                    OccurredAtUtc = fromUtc.AddHours(10).AddMinutes(5),
                    EventType = LandingTelemetryEventType.LeadSubmitSuccess
                },
                new LandingTelemetryEvent
                {
                    SessionId = providerSessionId,
                    VisitorId = "visitor-provider-001",
                    OccurredAtUtc = fromUtc.AddHours(11).AddMinutes(1),
                    EventType = LandingTelemetryEventType.Heartbeat,
                    ActiveSeconds = 10
                },
                new LandingTelemetryEvent
                {
                    SessionId = providerSessionId,
                    VisitorId = "visitor-provider-001",
                    OccurredAtUtc = fromUtc.AddHours(11).AddMinutes(2),
                    EventType = LandingTelemetryEventType.Click,
                    HeatmapRow = 0,
                    HeatmapColumn = 0,
                    ElementLabel = "Cadastrar-se como parceiro"
                }
            ]);

        leadRepositoryMock
            .Setup(repository => repository.GetByPeriodAsync(fromUtc, toUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new LandingLead
                {
                    Id = leadId,
                    SessionId = clientSessionId,
                    VisitorId = "visitor-client-001",
                    Origin = LandingLeadOrigin.Client,
                    FullName = "Leonardo Silva",
                    Email = "leo@teste.com",
                    Phone = "13999999999",
                    City = "Praia Grande",
                    State = "SP",
                    Neighborhood = "Ocian",
                    CreatedAt = fromUtc.AddHours(10).AddMinutes(6)
                }
            ]);

        runtimeSettingsMock
            .Setup(service => service.GetConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LandingAnalyticsRuntimeConfigDto
            {
                Clicks = new LandingClicksRuntimeConfigDto
                {
                    HeatmapGridRows = 6,
                    HeatmapGridColumns = 6
                }
            });

        var service = new AdminLandingAnalyticsService(
            accessRepositoryMock.Object,
            telemetryRepositoryMock.Object,
            leadRepositoryMock.Object,
            runtimeSettingsMock.Object);

        var overview = await service.GetOverviewAsync(new AdminLandingAnalyticsQueryDto(
            SearchTerm: null,
            Origin: null,
            Path: null,
            CountryCode: null,
            Region: null,
            City: null,
            FromUtc: fromUtc,
            ToUtc: toUtc,
            Page: 1,
            PageSize: 20));

        Assert.Equal(2, overview.TotalSessions);
        Assert.Equal(2, overview.TotalUniqueVisitors);
        Assert.Equal(25, overview.TotalActiveSeconds);
        Assert.Equal(2, overview.TotalClicks);
        Assert.Equal(1, overview.LeadSubmissions);
        Assert.Contains(overview.PathBreakdown, item => item.Label == "/Cliente" && item.Count == 1);
        Assert.Contains(overview.Heatmap, item => item.Row == 1 && item.Column == 2 && item.Hits == 1);
        Assert.Contains(overview.Sessions, item => item.SessionId == clientSessionId && item.LeadId == leadId);
    }

    [Fact(DisplayName = "Admin landing analytics service | Detalhe | Deve correlacionar acesso, timeline e lead por sessao")]
    public async Task GetSessionDetailsAsync_ShouldReturnCorrelatedSessionDetails()
    {
        var accessRepositoryMock = new Mock<ILandingAccessEventRepository>();
        var telemetryRepositoryMock = new Mock<ILandingTelemetryEventRepository>();
        var leadRepositoryMock = new Mock<ILandingLeadRepository>();
        var runtimeSettingsMock = new Mock<ILandingAnalyticsRuntimeSettings>();
        var sessionId = "session-client-001";
        var leadId = Guid.NewGuid();

        accessRepositoryMock
            .Setup(repository => repository.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LandingAccessEvent
            {
                SessionId = sessionId,
                VisitorId = "visitor-client-001",
                CreatedAt = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc),
                Path = "/Cliente",
                CurrentUrl = "https://www.consertapramim.com/Cliente",
                InitialLeadOrigin = LandingLeadOrigin.Client,
                IpAddress = "187.77.48.150",
                GeoCountry = "Brasil",
                GeoCountryCode = "BR",
                GeoRegion = "Sao Paulo",
                GeoRegionCode = "SP",
                GeoCity = "Praia Grande"
            });

        telemetryRepositoryMock
            .Setup(repository => repository.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new LandingTelemetryEvent
                {
                    SessionId = sessionId,
                    VisitorId = "visitor-client-001",
                    OccurredAtUtc = new DateTime(2026, 3, 1, 10, 1, 0, DateTimeKind.Utc),
                    EventType = LandingTelemetryEventType.Heartbeat,
                    ActiveSeconds = 15
                },
                new LandingTelemetryEvent
                {
                    SessionId = sessionId,
                    VisitorId = "visitor-client-001",
                    OccurredAtUtc = new DateTime(2026, 3, 1, 10, 2, 0, DateTimeKind.Utc),
                    EventType = LandingTelemetryEventType.Click,
                    ElementLabel = "Encontrar profissional"
                }
            ]);

        leadRepositoryMock
            .Setup(repository => repository.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LandingLead
            {
                Id = leadId,
                SessionId = sessionId,
                VisitorId = "visitor-client-001",
                Origin = LandingLeadOrigin.Client,
                FullName = "Leonardo Silva",
                Email = "leo@teste.com",
                Phone = "13999999999",
                City = "Praia Grande",
                State = "SP",
                Neighborhood = "Ocian",
                CreatedAt = new DateTime(2026, 3, 1, 10, 3, 0, DateTimeKind.Utc)
            });

        var service = new AdminLandingAnalyticsService(
            accessRepositoryMock.Object,
            telemetryRepositoryMock.Object,
            leadRepositoryMock.Object,
            runtimeSettingsMock.Object);

        var details = await service.GetSessionDetailsAsync(sessionId);

        Assert.NotNull(details);
        Assert.Equal("Praia Grande/SP", details!.EstimatedLocality);
        Assert.Equal("Praia Grande", details.Geo.City);
        Assert.NotNull(details.Lead);
        Assert.Equal(leadId, details.Lead!.Id);
        Assert.Equal("Ocian - Praia Grande/SP", details.Lead.Locality);
        Assert.Equal(4, details.Timeline.Count);
        Assert.Contains(details.Timeline, item => item.Type == "Acesso");
        Assert.Contains(details.Timeline, item => item.Type == "Lead captado");
    }
}
