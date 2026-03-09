using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Enums;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminFireTvDashboardServiceTests
{
    [Fact(DisplayName = "Admin fire tv dashboard service | Deve montar snapshot com 8 KPIs e listas operacionais")]
    public async Task GetLandingDashboardAsync_ShouldBuildSnapshotFromOverview()
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
                AutoRefreshSeconds = 45,
                SessionPageSize = 4,
                TopListSize = 3,
                ShowHeatmap = true,
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
            .Setup(service => service.GetOverviewAsync(It.IsAny<AdminLandingAnalyticsQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminLandingAnalyticsOverviewDto(
                FromUtc: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                ToUtc: new DateTime(2026, 3, 8, 0, 0, 0, DateTimeKind.Utc),
                TotalSessions: 12,
                TotalUniqueVisitors: 9,
                SessionsWithGeo: 10,
                TotalHeartbeats: 44,
                TotalActiveSeconds: 360,
                AverageActiveSecondsPerSession: 30,
                AverageMaxScrollPercent: 68.5,
                SessionsWithClicks: 8,
                TotalClicks: 25,
                LeadModalOpens: 7,
                LeadSubmissions: 3,
                LeadSubmissionRatePercent: 25,
                HeatmapRows: 6,
                HeatmapColumns: 6,
                Page: 1,
                PageSize: 4,
                TotalCount: 12,
                PathBreakdown: [],
                OriginBreakdown:
                [
                    new AdminLandingAnalyticsBreakdownItemDto("Cliente", 8),
                    new AdminLandingAnalyticsBreakdownItemDto("Prestador", 4)
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
                        "/Cliente",
                        LandingLeadOrigin.Client,
                        "Ocian - Praia Grande/SP",
                        40,
                        80,
                        8,
                        2,
                        1,
                        Guid.NewGuid()),
                    new AdminLandingAnalyticsSessionListItemDto(
                        "session-002",
                        "visitor-002",
                        new DateTime(2026, 3, 7, 11, 0, 0, DateTimeKind.Utc),
                        new DateTime(2026, 3, 7, 11, 4, 0, DateTimeKind.Utc),
                        "/Prestador",
                        LandingLeadOrigin.Provider,
                        "Aparecida - Santos/SP",
                        20,
                        55,
                        4,
                        1,
                        0,
                        null)
                ]));

        var service = new AdminFireTvDashboardService(
            analyticsServiceMock.Object,
            runtimeSettingsMock.Object);

        var result = await service.GetLandingDashboardAsync(7, CancellationToken.None);

        Assert.True(result.Enabled);
        Assert.Equal("ConsertaPraMim TV", result.AppTitle);
        Assert.Equal(8, result.Kpis.Count);
        Assert.Equal(45, result.AutoRefreshSeconds);
        Assert.Single(result.Heatmap);
        Assert.Equal(2, result.TopOrigins.Count);
        Assert.Equal(2, result.TopLocalities.Count);
        Assert.Equal(2, result.RecentSessions.Count);
        Assert.Contains(result.Kpis, item => item.Key == "leadSubmissionRatePercent" && item.Value == "25,0%");
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
                AllowedRangeDays = [7, 30]
            });

        analyticsServiceMock
            .Setup(service => service.GetOverviewAsync(It.IsAny<AdminLandingAnalyticsQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminLandingAnalyticsOverviewDto(
                FromUtc: new DateTime(2026, 2, 7, 0, 0, 0, DateTimeKind.Utc),
                ToUtc: new DateTime(2026, 3, 8, 0, 0, 0, DateTimeKind.Utc),
                TotalSessions: 0,
                TotalUniqueVisitors: 0,
                SessionsWithGeo: 0,
                TotalHeartbeats: 0,
                TotalActiveSeconds: 0,
                AverageActiveSecondsPerSession: 0,
                AverageMaxScrollPercent: 0,
                SessionsWithClicks: 0,
                TotalClicks: 0,
                LeadModalOpens: 0,
                LeadSubmissions: 0,
                LeadSubmissionRatePercent: 0,
                HeatmapRows: 0,
                HeatmapColumns: 0,
                Page: 1,
                PageSize: 6,
                TotalCount: 0,
                PathBreakdown: [],
                OriginBreakdown: [],
                CountryBreakdown: [],
                RegionBreakdown: [],
                CityBreakdown: [],
                EventBreakdown: [],
                Heatmap: [],
                Sessions: []));

        var service = new AdminFireTvDashboardService(
            analyticsServiceMock.Object,
            runtimeSettingsMock.Object);

        var result = await service.GetLandingDashboardAsync(1, CancellationToken.None);

        Assert.Equal(30, result.SelectedRangeDays);
        analyticsServiceMock.Verify(service => service.GetOverviewAsync(
            It.Is<AdminLandingAnalyticsQueryDto>(query =>
                query.FromUtc.HasValue &&
                query.ToUtc.HasValue &&
                (query.ToUtc.Value - query.FromUtc.Value).TotalDays >= 29.9),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
