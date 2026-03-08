using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using Moq;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Services;

public class LandingTelemetryEventServiceTests
{
    [Fact(DisplayName = "Landing telemetry event service | Batch | Deve persistir heartbeat e clique com heatmap normalizado")]
    public async Task RecordBatchAsync_ShouldPersistNormalizedEvents()
    {
        var repositoryMock = new Mock<ILandingTelemetryEventRepository>();
        var runtimeSettingsMock = new Mock<ILandingAnalyticsRuntimeSettings>();
        IReadOnlyCollection<LandingTelemetryEvent>? persistedEvents = null;

        runtimeSettingsMock
            .Setup(service => service.GetConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LandingAnalyticsRuntimeConfigDto
            {
                ClientTelemetryEnabled = true,
                Clicks = new LandingClicksRuntimeConfigDto
                {
                    HeatmapGridRows = 4,
                    HeatmapGridColumns = 5
                }
            });

        repositoryMock
            .Setup(repository => repository.AddRangeAsync(It.IsAny<IReadOnlyCollection<LandingTelemetryEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<LandingTelemetryEvent>, CancellationToken>((events, _) => persistedEvents = events)
            .Returns(Task.CompletedTask);

        var service = new LandingTelemetryEventService(repositoryMock.Object, runtimeSettingsMock.Object);

        var request = new RecordLandingTelemetryBatchRequestDto(
            VisitorId: "visitor-telemetry-001",
            SessionId: "session-telemetry-001",
            CurrentUrl: "https://www.consertapramim.com/Cliente",
            Path: "/Cliente",
            Host: "www.consertapramim.com",
            Scheme: "https",
            InitialLeadOrigin: "client",
            ViewportWidth: 1280,
            ViewportHeight: 720,
            BrowserLanguage: "pt-BR",
            Events:
            [
                new RecordLandingTelemetryEventItemDto(
                    Type: "heartbeat",
                    OccurredAtUtc: DateTime.UtcNow.AddSeconds(-5),
                    ActiveSeconds: 15,
                    ScrollDepthPercent: null,
                    ClickXPercent: null,
                    ClickYPercent: null,
                    HeatmapRow: null,
                    HeatmapColumn: null,
                    ElementKey: null,
                    ElementLabel: null,
                    ElementHref: null),
                new RecordLandingTelemetryEventItemDto(
                    Type: "click",
                    OccurredAtUtc: DateTime.UtcNow.AddSeconds(-3),
                    ActiveSeconds: null,
                    ScrollDepthPercent: null,
                    ClickXPercent: 62,
                    ClickYPercent: 10,
                    HeatmapRow: null,
                    HeatmapColumn: null,
                    ElementKey: "cta-client",
                    ElementLabel: "Encontrar profissional",
                    ElementHref: "https://www.consertapramim.com/Cliente"),
                new RecordLandingTelemetryEventItemDto(
                    Type: "unknown",
                    OccurredAtUtc: DateTime.UtcNow,
                    ActiveSeconds: null,
                    ScrollDepthPercent: null,
                    ClickXPercent: null,
                    ClickYPercent: null,
                    HeatmapRow: null,
                    HeatmapColumn: null,
                    ElementKey: null,
                    ElementLabel: null,
                    ElementHref: null)
            ]);

        var context = new LandingLeadCaptureContextDto(
            IpAddress: "187.77.48.150",
            ForwardedFor: "187.77.48.150",
            UserAgent: "Mozilla/5.0",
            AcceptLanguage: "pt-BR",
            Host: "api.consertapramim.com",
            Scheme: "https",
            Path: "/api/landing-analytics/public/events",
            RefererHeader: "https://www.consertapramim.com/");

        var response = await service.RecordBatchAsync(request, context, CancellationToken.None);

        Assert.Equal(2, response.AcceptedEvents);
        Assert.NotNull(persistedEvents);
        Assert.Collection(
            persistedEvents!,
            heartbeat =>
            {
                Assert.Equal("visitor-telemetry-001", heartbeat.VisitorId);
                Assert.Equal("session-telemetry-001", heartbeat.SessionId);
                Assert.Equal(15, heartbeat.ActiveSeconds);
                Assert.Equal("pt-BR", heartbeat.BrowserLanguage);
            },
            click =>
            {
                Assert.Equal(3, click.HeatmapColumn);
                Assert.Equal(0, click.HeatmapRow);
                Assert.Equal("cta-client", click.ElementKey);
                Assert.Equal("Encontrar profissional", click.ElementLabel);
            });
    }
}
