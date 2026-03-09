using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
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

        var service = CreateService(
            analyticsService: analyticsServiceMock.Object,
            runtimeSettings: runtimeSettingsMock.Object);

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

        var service = CreateService(
            analyticsService: analyticsServiceMock.Object,
            runtimeSettings: runtimeSettingsMock.Object);

        var result = await service.GetLandingDashboardAsync(1, "all", "none", CancellationToken.None);

        Assert.Equal(30, result.SelectedRangeDays);
        analyticsServiceMock.Verify(service => service.GetInsightsAsync(
            It.Is<AdminLandingAnalyticsQueryDto>(query =>
                query.FromUtc.HasValue &&
                query.ToUtc.HasValue &&
                (query.ToUtc.Value - query.FromUtc.Value).TotalDays >= 29.9),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Admin fire tv dashboard service | Deve montar visao operacional com health, mapa e serie diaria")]
    public async Task GetOperationsDashboardAsync_ShouldBuildOperationalSnapshot()
    {
        var runtimeSettingsMock = new Mock<IFireTvDashboardRuntimeSettings>();
        runtimeSettingsMock
            .Setup(service => service.GetConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FireTvDashboardRuntimeConfigDto
            {
                Enabled = true,
                OperationsHistoryDays = 7,
                OperationsRefreshSeconds = 5,
                SignalRPulseSeconds = 5,
                OperationsMapMaxProviders = 8,
                OperationsMapMaxRequests = 8,
                OperationsRecentActivitySize = 4,
                OperationsHealthCheckTimeoutMs = 2000,
                HealthTargets =
                [
                    new FireTvDashboardHealthTargetConfigDto("api", "API", "https://api.consertapramim.com/health"),
                    new FireTvDashboardHealthTargetConfigDto("admin", "Admin", "https://admin.consertapramim.com")
                ]
            });

        var dashboardServiceMock = new Mock<IAdminDashboardService>();
        dashboardServiceMock
            .Setup(service => service.GetDashboardAsync(It.IsAny<AdminDashboardQueryDto>()))
            .ReturnsAsync(new AdminDashboardDto(
                TotalUsers: 20,
                ActiveUsers: 20,
                InactiveUsers: 0,
                TotalProviders: 12,
                TotalClients: 8,
                OnlineProviders: 5,
                OnlineClients: 1,
                PayingProviders: 6,
                MonthlySubscriptionRevenue: 3250m,
                RevenueByPlan: [],
                TotalAdmins: 2,
                TotalRequests: 40,
                ActiveRequests: 6,
                RequestsInPeriod: 11,
                RequestsByStatus: [],
                RequestsByCategory:
                [
                    new AdminCategoryCountDto("Eletricista", 5),
                    new AdminCategoryCountDto("Encanador", 3),
                    new AdminCategoryCountDto("Chaveiro", 2)
                ],
                ProposalsInPeriod: 0,
                AcceptedProposalsInPeriod: 0,
                ActiveChatConversationsLast24h: 0,
                FromUtc: DateTime.UtcNow.AddDays(-7),
                ToUtc: DateTime.UtcNow,
                Page: 1,
                PageSize: 20,
                TotalEvents: 0,
                RecentEvents: [],
                AppointmentConfirmationInSlaRatePercent: 92.5m));
        dashboardServiceMock
            .Setup(service => service.GetCoverageMapAsync(It.IsAny<string?>()))
            .ReturnsAsync(new AdminCoverageMapDto(
                Providers:
                [
                    new AdminCoverageMapProviderDto(Guid.NewGuid(), "Juliana", "Praia Grande", -24.015d, -46.412d, 5d, nameof(ProviderOperationalStatus.Online), true),
                    new AdminCoverageMapProviderDto(Guid.NewGuid(), "Carlos", "Santos", -23.981d, -46.333d, 6d, nameof(ProviderOperationalStatus.EmAtendimento), true)
                ],
                Requests:
                [
                    new AdminCoverageMapRequestDto(Guid.NewGuid(), ServiceRequestStatus.Matching.ToString(), "Eletricista", "Sem energia", "Praia Grande", "Ocian", "Rua A", -24.020d, -46.420d, DateTime.UtcNow.AddMinutes(-15)),
                    new AdminCoverageMapRequestDto(Guid.NewGuid(), ServiceRequestStatus.InProgress.ToString(), "Encanador", "Vazamento", "Santos", "Aparecida", "Rua B", -23.990d, -46.320d, DateTime.UtcNow.AddMinutes(-8))
                ],
                GeneratedAtUtc: DateTime.UtcNow,
                AvailableProviderCities: ["Praia Grande", "Santos"]));

        var healthProbeMock = new Mock<IFireTvDashboardHealthProbe>();
        healthProbeMock
            .Setup(service => service.ProbeAsync(It.IsAny<IReadOnlyList<FireTvDashboardHealthTargetConfigDto>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new AdminFireTvHealthTargetStatusDto("api", "API", "https://api.consertapramim.com/health", true, 42, "OK", null),
                new AdminFireTvHealthTargetStatusDto("admin", "Admin", "https://admin.consertapramim.com", true, 51, "OK", null)
            ]);

        var users = new List<User>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Role = UserRole.Provider,
                Name = "Juliana",
                ProviderProfile = new ProviderProfile
                {
                    Rating = 4.8,
                    ReviewCount = 8,
                    OperationalStatus = ProviderOperationalStatus.Online
                }
            },
            new()
            {
                Id = Guid.NewGuid(),
                Role = UserRole.Provider,
                Name = "Carlos",
                ProviderProfile = new ProviderProfile
                {
                    Rating = 4.5,
                    ReviewCount = 4,
                    OperationalStatus = ProviderOperationalStatus.EmAtendimento
                }
            }
        };

        var requests = new List<ServiceRequest>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Category = ServiceCategory.Electrical,
                Status = ServiceRequestStatus.Created,
                AddressCity = "Praia Grande",
                AddressNeighborhood = "Ocian",
                AddressStreet = "Rua A",
                Latitude = -24.02,
                Longitude = -46.42,
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-30),
                Appointments =
                [
                    new ServiceAppointment
                    {
                        Id = Guid.NewGuid(),
                        CreatedAt = DateTime.UtcNow.AddHours(-1),
                        Status = ServiceAppointmentStatus.Confirmed
                    }
                ]
            },
            new()
            {
                Id = Guid.NewGuid(),
                Category = ServiceCategory.Plumbing,
                Status = ServiceRequestStatus.Completed,
                AddressCity = "Santos",
                AddressNeighborhood = "Aparecida",
                AddressStreet = "Rua B",
                Latitude = -23.99,
                Longitude = -46.32,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-10),
                Appointments =
                [
                    new ServiceAppointment
                    {
                        Id = Guid.NewGuid(),
                        CreatedAt = DateTime.UtcNow.AddDays(-1),
                        Status = ServiceAppointmentStatus.Completed,
                        CompletedAtUtc = DateTime.UtcNow.AddMinutes(-12)
                    }
                ]
            },
            new()
            {
                Id = Guid.NewGuid(),
                Category = ServiceCategory.Other,
                Status = ServiceRequestStatus.Canceled,
                AddressCity = "Guaruja",
                AddressNeighborhood = "Centro",
                AddressStreet = "Rua C",
                Latitude = -23.99,
                Longitude = -46.25,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
            }
        };

        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock.Setup(service => service.GetAllAsync()).ReturnsAsync(users);
        var requestRepositoryMock = new Mock<IServiceRequestRepository>();
        requestRepositoryMock.Setup(service => service.GetAllAsync()).ReturnsAsync(requests);

        var service = CreateService(
            runtimeSettings: runtimeSettingsMock.Object,
            adminDashboardService: dashboardServiceMock.Object,
            healthProbe: healthProbeMock.Object,
            userRepository: userRepositoryMock.Object,
            requestRepository: requestRepositoryMock.Object);

        var result = await service.GetOperationsDashboardAsync(CancellationToken.None);

        Assert.True(result.Enabled);
        Assert.Equal(5, result.RefreshSeconds);
        Assert.Equal(5, result.PulseSeconds);
        Assert.Equal(8, result.Kpis.Count);
        Assert.Equal("online", result.OverallStatus);
        Assert.Equal(2, result.HealthTargets.Count);
        Assert.NotEmpty(result.Categories);
        Assert.NotEmpty(result.ProviderPoints);
        Assert.NotEmpty(result.RequestPoints);
        Assert.NotEmpty(result.DailySeries);
        Assert.NotEmpty(result.RecentActivity);
        Assert.Contains(result.Kpis, item => item.Key == "monthlySubscriptionRevenue" && item.Value.Contains("3.250"));
        Assert.Contains(result.Kpis, item => item.Key == "sla" && item.Value.Contains("92,5"));
    }

    private static AdminFireTvDashboardService CreateService(
        IAdminLandingAnalyticsService? analyticsService = null,
        IFireTvDashboardRuntimeSettings? runtimeSettings = null,
        IAdminDashboardService? adminDashboardService = null,
        IFireTvDashboardHealthProbe? healthProbe = null,
        IUserRepository? userRepository = null,
        IServiceRequestRepository? requestRepository = null)
    {
        return new AdminFireTvDashboardService(
            analyticsService ?? Mock.Of<IAdminLandingAnalyticsService>(),
            adminDashboardService ?? Mock.Of<IAdminDashboardService>(),
            runtimeSettings ?? Mock.Of<IFireTvDashboardRuntimeSettings>(),
            healthProbe ?? Mock.Of<IFireTvDashboardHealthProbe>(),
            userRepository ?? Mock.Of<IUserRepository>(),
            requestRepository ?? Mock.Of<IServiceRequestRepository>());
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
