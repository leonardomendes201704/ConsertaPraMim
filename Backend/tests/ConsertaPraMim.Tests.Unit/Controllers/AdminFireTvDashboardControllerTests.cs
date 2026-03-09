using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class AdminFireTvDashboardControllerTests
{
    [Fact(DisplayName = "Admin fire tv dashboard controller | Deve retornar snapshot ok")]
    public async Task Get_ShouldReturnOk()
    {
        var serviceMock = new Mock<IAdminFireTvDashboardService>();
        var payload = new AdminFireTvLandingDashboardDto(
            Enabled: true,
            AppTitle: "ConsertaPraMim TV",
            AppSubtitle: "Landing",
            SelectedRangeDays: 7,
            SelectedOrigin: "all",
            SelectedComparisonMode: "previous_period",
            AllowedRangeDays: [1, 7, 30],
            OriginOptions:
            [
                new AdminFireTvDashboardFilterOptionDto("all", "Todas as origens")
            ],
            ComparisonOptions:
            [
                new AdminFireTvDashboardFilterOptionDto("previous_period", "Periodo anterior")
            ],
            ShowComparison: true,
            AutoRefreshSeconds: 30,
            GeneratedAtUtc: new DateTime(2026, 3, 8, 12, 0, 0, DateTimeKind.Utc),
            FromUtc: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 3, 8, 0, 0, 0, DateTimeKind.Utc),
            ComparisonFromUtc: new DateTime(2026, 2, 23, 0, 0, 0, DateTimeKind.Utc),
            ComparisonToUtc: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            ComparisonLabel: "vs 7 dia(s) anteriores",
            Kpis:
            [
                new AdminFireTvDashboardKpiDto("totalSessions", "Sessoes", "12", "Periodo", "primary", "8", "+50,0%", "vs 7 dia(s) anteriores", "success")
            ],
            ShowHeatmap: true,
            HeatmapRows: 6,
            HeatmapColumns: 6,
            Heatmap: [],
            ShowScrollmap: true,
            Scrollmap: [],
            ShowElementRanking: true,
            TopElements: [],
            TopOrigins: [],
            TopLocalities: [],
            RecentSessions: []);

        serviceMock
            .Setup(service => service.GetLandingDashboardAsync(7, "client", "previous_period", It.IsAny<CancellationToken>()))
            .ReturnsAsync(payload);

        var controller = new AdminFireTvDashboardController(serviceMock.Object);

        var result = await controller.Get(7, "client", "previous_period", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(payload, ok.Value);
    }

    [Fact(DisplayName = "Admin fire tv operations dashboard controller | Deve retornar snapshot operacional ok")]
    public async Task GetOperations_ShouldReturnOk()
    {
        var serviceMock = new Mock<IAdminFireTvDashboardService>();
        var payload = new AdminFireTvOperationsDashboardDto(
            Enabled: true,
            AppTitle: "ConsertaPraMim TV",
            AppSubtitle: "Visao operacional",
            RefreshSeconds: 5,
            PulseSeconds: 5,
            HistoryDays: 7,
            GeneratedAtUtc: new DateTime(2026, 3, 9, 12, 0, 0, DateTimeKind.Utc),
            RealtimeConnected: true,
            OverallStatus: "online",
            AverageLatencyMs: 42,
            HealthyTargets: 4,
            TotalTargets: 4,
            HealthTargets:
            [
                new AdminFireTvHealthTargetStatusDto("api", "API", "https://api.consertapramim.com/health", true, 42, "OK", null)
            ],
            Kpis:
            [
                new AdminFireTvDashboardKpiDto("servicesToday", "Servicos hoje", "152", "Pedidos abertos hoje", "primary", null, null, null, null)
            ],
            Categories:
            [
                new AdminFireTvOperationalCategoryDto("Eletricista", 12, 28)
            ],
            ProviderPoints:
            [
                new AdminFireTvOperationalMapPointDto(Guid.NewGuid(), "provider", "Juliana", "Praia Grande", -24.01, -46.41, "success")
            ],
            RequestPoints:
            [
                new AdminFireTvOperationalMapPointDto(Guid.NewGuid(), "request", "Eletricista", "Ocian - Praia Grande", -24.02, -46.42, "warning")
            ],
            DailySeries:
            [
                new AdminFireTvOperationalDailySeriesItemDto("09/03", 14, 9)
            ],
            RecentActivity:
            [
                new AdminFireTvOperationalRecentActivityDto("10:21", "Eletricista - Praia Grande", "Em matching • Ocian - Praia Grande", "warning")
            ]);

        serviceMock
            .Setup(service => service.GetOperationsDashboardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(payload);

        var controller = new AdminFireTvOperationsDashboardController(serviceMock.Object);

        var result = await controller.Get(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(payload, ok.Value);
    }
}
