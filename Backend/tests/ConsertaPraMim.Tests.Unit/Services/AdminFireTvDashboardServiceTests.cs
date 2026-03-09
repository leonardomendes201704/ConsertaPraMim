using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Enums;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminFireTvDashboardServiceTests
{
    [Fact(DisplayName = "Admin fire tv dashboard service | Deve montar snapshot fase 2 com comparacao, scrollmap e ranking")]
    public async Task GetLandingDashboardAsync_ShouldBuildPhaseTwoSnapshot()
    {
        var analyticsServiceMock = new Mock<IAdminLandingAnalyticsService>();
        var runtimeSettingsMock = new Mock<IFireTvDashboardRuntimeSettings>();

        runtimeSettingsMock
            .Setup(service => service.GetConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FireTvDashboardRuntimeConfigDto
            {
                Enabled = true,
                AppTitle = "ConsertaPraMim TV",
                AppSubtitle = "Landing",
                DefaultRangeDays = 7,
                AllowedRangeDays = [1, 7, 30],
                DefaultOriginFilter = "all",
                OriginFilters =
                [
                    new FireTvDashboardFilterOptionConfigDto("all", "Todas as origens"),
                    new FireTvDashboardFilterOptionConfigDto("client", "Cliente"),
                    new FireTvDashboardFilterOptionConfigDto("provider", "Prestador")
                ],
                DefaultComparisonMode = "previous_period",
                ComparisonModes =
                [
                    new FireTvDashboardFilterOptionConfigDto("none", "Sem comparacao"),
                    new FireTvDashboardFilterOptionConfigDto("previous_period", "Periodo anterior")
                ],
                AutoRefreshSeconds = 45,
                SessionPageSize = 4,
                TopListSize = 3,
                ShowHeatmap = true,
                ShowComparison = true,
                ShowScrollmap = true,
                ShowElementRanking = true,
                ElementRankingSize = 4,
                KpiKeys =
                [
                    "totalSessions",
                    "uniqueVisitors",
                    "leadSubmissions",
                    "leadSubmissionRatePercent",
                    "leadModalOpens",
                    "totalClicks",
                    "averageActiveSecondsPerSession",
                    "averageMaxScrollPercent"
                ]
            });

        analyticsServiceMock
            .SetupSequence(service => service.GetInsightsAsync(It.IsAny<AdminLandingAnalyticsQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildInsights(
                totalSessions: 12,
                totalUniqueVisitors: 9,
                totalClicks: 25,
                leadSubmissions: 3,
                leadModalOpens: 7,
                averageActiveSeconds: 30,
                averageMaxScrollPercent: 68.5,
                sessionOrigin: LandingLeadOrigin.Client,
                locality: "Ocian - Praia Grande/SP",
                includeLead: true))
            .ReturnsAsync(BuildInsights(
                totalSessions: 8,
                totalUniqueVisitors: 7,
                totalClicks: 10,
                leadSubmissions: 1,
                leadModalOpens: 4,
                averageActiveSeconds: 20,
                averageMaxScrollPercent: 52,
                sessionOrigin: LandingLeadOrigin.Client,
                locality: "Aparecida - Santos/SP",
                includeLead: false));

        var service = new AdminFireTvDashboardService(
            analyticsServiceMock.Object,
            runtimeSettingsMock.Object);

        var result = await service.GetLandingDashboardAsync(7, "client", "previous_period", CancellationToken.None);

        Assert.True(result.Enabled);
        Assert.Equal("client", result.SelectedOrigin);
        Assert.Equal("previous_period", result.SelectedComparisonMode);
        Assert.Equal("ConsertaPraMim TV", result.AppTitle);
        Assert.Equal(8, result.Kpis.Count);
        Assert.Equal(45, result.AutoRefreshSeconds);
        Assert.True(result.ShowScrollmap);
        Assert.True(result.ShowElementRanking);
        Assert.NotNull(result.ComparisonLabel);
        Assert.Single(result.Heatmap);
        Assert.Equal(2, result.TopOrigins.Count);
        Assert.Single(result.TopLocalities);
        Assert.Equal(2, result.Scrollmap.Count);
        Assert.Equal(2, result.TopElements.Count);
        Assert.Single(result.RecentSessions);
        Assert.Contains(result.Kpis, item => item.Key == "leadSubmissionRatePercent" && item.ComparisonValue != null);
        Assert.Contains(result.TopElements, item => item.Label == "Encontrar profissional" && item.Clicks == 6);
    }

    [Fact(DisplayName = "Admin fire tv dashboard service | Deve respeitar range permitido do runtime")]
    public async Task GetLandingDashboardAsync_ShouldFallbackToDefaultRange_WhenRequestedRangeIsNotAllowed()
    {
        var analyticsServiceMock = new Mock<IAdminLandingAnalyticsService>();
        var runtimeSettingsMock = new Mock<IFireTvDashboardRuntimeSettings>();

        runtimeSettingsMock
            .Setup(service => service.GetConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FireTvDashboardRuntimeConfigDto
            {
                DefaultRangeDays = 30,
                AllowedRangeDays = [7, 30],
                DefaultComparisonMode = "none",
                ComparisonModes =
                [
                    new FireTvDashboardFilterOptionConfigDto("none", "Sem comparacao")
                ]
            });

        analyticsServiceMock
            .Setup(service => service.GetInsightsAsync(It.IsAny<AdminLandingAnalyticsQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildInsights(
                totalSessions: 0,
                totalUniqueVisitors: 0,
                totalClicks: 0,
                leadSubmissions: 0,
                leadModalOpens: 0,
                averageActiveSeconds: 0,
                averageMaxScrollPercent: 0,
                sessionOrigin: null,
                locality: "Nao mapeado",
                includeLead: false));

        var service = new AdminFireTvDashboardService(
            analyticsServiceMock.Object,
            runtimeSettingsMock.Object);

        var result = await service.GetLandingDashboardAsync(1, "all", "none", CancellationToken.None);

        Assert.Equal(30, result.SelectedRangeDays);
        analyticsServiceMock.Verify(service => service.GetInsightsAsync(
            It.Is<AdminLandingAnalyticsQueryDto>(query =>
                query.FromUtc.HasValue &&
                query.ToUtc.HasValue &&
                (query.ToUtc.Value - query.FromUtc.Value).TotalDays >= 29.9),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AdminLandingAnalyticsInsightsDto BuildInsights(
        int totalSessions,
        int totalUniqueVisitors,
        int totalClicks,
        int leadSubmissions,
        int leadModalOpens,
        double averageActiveSeconds,
        double averageMaxScrollPercent,
        LandingLeadOrigin? sessionOrigin,
        string locality,
        bool includeLead)
    {
        Guid? sessionLeadId = includeLead ? Guid.NewGuid() : null;
        var overview = new AdminLandingAnalyticsOverviewDto(
            FromUtc: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 3, 8, 0, 0, 0, DateTimeKind.Utc),
            TotalSessions: totalSessions,
            TotalUniqueVisitors: totalUniqueVisitors,
            SessionsWithGeo: Math.Min(totalSessions, Math.Max(totalSessions - 2, 0)),
            TotalHeartbeats: totalSessions * 2,
            TotalActiveSeconds: (int)Math.Round(totalSessions * averageActiveSeconds, MidpointRounding.AwayFromZero),
            AverageActiveSecondsPerSession: averageActiveSeconds,
            AverageMaxScrollPercent: averageMaxScrollPercent,
            SessionsWithClicks: totalClicks > 0 ? Math.Min(totalSessions, totalClicks) : 0,
            TotalClicks: totalClicks,
            LeadModalOpens: leadModalOpens,
            LeadSubmissions: leadSubmissions,
            LeadSubmissionRatePercent: totalSessions == 0 ? 0 : Math.Round((leadSubmissions * 100d) / totalSessions, 1, MidpointRounding.AwayFromZero),
            HeatmapRows: 6,
            HeatmapColumns: 6,
            Page: 1,
            PageSize: 4,
            TotalCount: totalSessions,
            PathBreakdown: [],
            OriginBreakdown:
            [
                new AdminLandingAnalyticsBreakdownItemDto("Cliente", Math.Max(1, totalSessions / 2)),
                new AdminLandingAnalyticsBreakdownItemDto("Prestador", Math.Max(1, totalSessions / 3))
            ],
            CountryBreakdown: [],
            RegionBreakdown: [],
            CityBreakdown: [],
            EventBreakdown: [],
            Heatmap:
            [
                new AdminLandingAnalyticsHeatmapCellDto(1, 2, 5)
            ],
            Sessions:
            [
                new AdminLandingAnalyticsSessionListItemDto(
                    "session-001",
                    "visitor-001",
                    new DateTime(2026, 3, 7, 10, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 3, 7, 10, 10, 0, DateTimeKind.Utc),
                    sessionOrigin == LandingLeadOrigin.Provider ? "/Prestador" : "/Cliente",
                    sessionOrigin,
                    locality,
                    (int)Math.Round(averageActiveSeconds, MidpointRounding.AwayFromZero),
                    (int)Math.Round(averageMaxScrollPercent, MidpointRounding.AwayFromZero),
                    Math.Max(totalClicks, 0),
                    leadModalOpens,
                    leadSubmissions,
                    sessionLeadId)
            ]);

        return new AdminLandingAnalyticsInsightsDto(
            overview,
            [
                new AdminLandingAnalyticsScrollmapBucketDto(50, Math.Max(0, totalSessions - 1), totalSessions == 0 ? 0 : 75),
                new AdminLandingAnalyticsScrollmapBucketDto(100, Math.Max(0, totalSessions / 3), totalSessions == 0 ? 0 : 33.3)
            ],
            [
                new AdminLandingAnalyticsElementRankingItemDto("lead-trigger:client", "Encontrar profissional", "/Cliente", 6, Math.Max(1, totalSessions / 2), 50),
                new AdminLandingAnalyticsElementRankingItemDto("lead-trigger:provider", "Cadastrar-se como parceiro", "/Prestador", 4, Math.Max(1, totalSessions / 3), 33.3)
            ]);
    }
}
