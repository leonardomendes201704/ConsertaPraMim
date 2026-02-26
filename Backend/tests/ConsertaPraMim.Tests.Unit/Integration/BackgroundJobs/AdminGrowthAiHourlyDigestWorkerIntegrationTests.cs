using ConsertaPraMim.API.BackgroundJobs;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integration.BackgroundJobs;

public class AdminGrowthAiHourlyDigestWorkerIntegrationTests
{
    [Fact(DisplayName = "Admin growth ai hourly digest worker | Run once | Deve enviar relatorio para destinatario principal e copia")]
    public async Task RunOnceAsync_ShouldSendDigestToPrimaryAndCc()
    {
        var dashboardService = new Mock<IAdminDashboardService>();
        var monitoringService = new Mock<IAdminMonitoringService>();
        var growthAiService = new Mock<IAdminGrowthAiService>();
        var growthAiStore = new Mock<IAdminGrowthAiStore>();
        var growthAiGateway = new Mock<IAdminGrowthAiGateway>();
        var mailboxStore = new Mock<IAdminMailboxStore>();
        var mailboxGateway = new Mock<IAdminMailboxGateway>();

        var nowUtc = DateTime.UtcNow;
        dashboardService
            .Setup(service => service.GetDashboardAsync(It.IsAny<AdminDashboardQueryDto>()))
            .ReturnsAsync(CreateDashboard(nowUtc));

        monitoringService
            .Setup(service => service.GetOverviewAsync(It.IsAny<AdminMonitoringOverviewQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMonitoringOverview(nowUtc));

        growthAiService
            .Setup(service => service.AnalyzeAsync(
                It.IsAny<AdminGrowthAiAnalyzeRequestDto>(),
                It.IsAny<Guid>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminGrowthAiAnalyzeResultDto(
                Success: true,
                Analysis: new AdminGrowthAiAnalysisDto(
                    AnalysisId: Guid.NewGuid(),
                    CreatedAtUtc: nowUtc,
                    ActorEmail: "system@teste.com",
                    FromUtc: nowUtc.AddHours(-2),
                    ToUtc: nowUtc,
                    Category: null,
                    City: null,
                    ExecutiveSummary: "Resumo diario de growth.",
                    FunnelInsights: ["Insight 1"],
                    LiquidityInsights: ["Insight 2"],
                    Risks: ["Risco 1"],
                    RecommendedActions: ["Acao 1"],
                    Model: "gpt-4.1-mini",
                    InputTokens: 200,
                    OutputTokens: 120,
                    TotalTokens: 320)));

        growthAiStore
            .Setup(store => store.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminGrowthAiStoreSnapshot(
                Settings: new AdminGrowthAiStoreSettings(
                    Enabled: true,
                    Provider: "OpenAI",
                    Model: "gpt-4.1-mini",
                    ApiKey: "test-api-key",
                    Temperature: 0.2m,
                    MaxOutputTokens: 900,
                    SystemPrompt: "Prompt",
                    UpdatedAtUtc: nowUtc),
                Analyses: Array.Empty<AdminGrowthAiAnalysisDto>()));

        growthAiGateway
            .Setup(gateway => gateway.GenerateAnalysisAsync(It.IsAny<AdminGrowthAiGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminGrowthAiGatewayResult(
                Success: true,
                OutputText: "<section><h1>Relatorio IA</h1><p>Conteudo.</p></section>",
                InputTokens: 1200,
                OutputTokens: 600,
                TotalTokens: 1800));

        mailboxStore
            .Setup(store => store.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminMailboxStoreSnapshot(
                Settings: new AdminMailboxStoreSettings(
                    Enabled: true,
                    SenderDisplayName: "ConsertaPraMim",
                    SenderEmail: "consertapramimappoficial@gmail.com",
                    Username: "consertapramimappoficial@gmail.com",
                    Password: "app-password",
                    SmtpHost: "smtp.gmail.com",
                    SmtpPort: 587,
                    SmtpUseSsl: true,
                    Pop3Host: "pop.gmail.com",
                    Pop3Port: 995,
                    Pop3UseSsl: true,
                    SyncWindowSize: 40,
                    PollIntervalSeconds: 120,
                    UpdatedAtUtc: nowUtc),
                Messages: Array.Empty<AdminMailboxStoredMessage>(),
                SyncState: new AdminMailboxStoreSyncState(null, null, null)));

        var sentRecipients = new List<string>();
        mailboxGateway
            .Setup(gateway => gateway.SendAsync(It.IsAny<AdminMailboxGatewaySendRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AdminMailboxGatewaySendRequest, CancellationToken>((request, _) => sentRecipients.Add(request.To))
            .Returns(Task.CompletedTask);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminGrowthAi:HourlyDigest:Enabled"] = "true",
                ["AdminGrowthAi:HourlyDigest:TimeZoneId"] = "America/Sao_Paulo",
                ["AdminGrowthAi:HourlyDigest:PrimaryRecipient"] = "devcraftstudio@outlook.com",
                ["AdminGrowthAi:HourlyDigest:CcRecipients"] = "leonardomendes201704@gmail.com",
                ["AdminGrowthAi:HourlyDigest:SubjectPrefix"] = "[ConsertaPraMim] Relatorio horario IA"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(dashboardService.Object);
        services.AddSingleton(monitoringService.Object);
        services.AddSingleton(growthAiService.Object);
        services.AddSingleton(growthAiStore.Object);
        services.AddSingleton(growthAiGateway.Object);
        services.AddSingleton(mailboxStore.Object);
        services.AddSingleton(mailboxGateway.Object);
        services.AddLogging();

        await using var provider = services.BuildServiceProvider();
        var worker = new AdminGrowthAiHourlyDigestWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            configuration,
            NullLogger<AdminGrowthAiHourlyDigestWorker>.Instance);

        await worker.RunOnceAsync();

        growthAiService.Verify(service => service.AnalyzeAsync(
            It.IsAny<AdminGrowthAiAnalyzeRequestDto>(),
            It.IsAny<Guid>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        Assert.Contains("devcraftstudio@outlook.com", sentRecipients, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("leonardomendes201704@gmail.com", sentRecipients, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(2, sentRecipients.Count);
    }

    private static AdminDashboardDto CreateDashboard(DateTime nowUtc)
    {
        return new AdminDashboardDto(
            TotalUsers: 100,
            ActiveUsers: 80,
            InactiveUsers: 20,
            TotalProviders: 40,
            TotalClients: 60,
            OnlineProviders: 12,
            OnlineClients: 20,
            PayingProviders: 25,
            MonthlySubscriptionRevenue: 12500m,
            RevenueByPlan: Array.Empty<AdminPlanRevenueDto>(),
            TotalAdmins: 2,
            TotalRequests: 320,
            ActiveRequests: 84,
            RequestsInPeriod: 120,
            RequestsByStatus: [new AdminStatusCountDto("Open", 84)],
            RequestsByCategory: [new AdminCategoryCountDto("Eletrica", 22)],
            ProposalsInPeriod: 66,
            AcceptedProposalsInPeriod: 33,
            ActiveChatConversationsLast24h: 19,
            FromUtc: nowUtc.AddHours(-24),
            ToUtc: nowUtc,
            Page: 1,
            PageSize: 20,
            TotalEvents: 1,
            RecentEvents:
            [
                new AdminRecentEventDto(
                    Type: "admin_event_client_login",
                    ReferenceId: Guid.NewGuid(),
                    CreatedAt: nowUtc.AddMinutes(-30),
                    Title: "Cliente fez login",
                    Description: "Cliente 02")
            ]);
    }

    private static AdminMonitoringOverviewDto CreateMonitoringOverview(DateTime nowUtc)
    {
        return new AdminMonitoringOverviewDto(
            TotalRequests: 1024,
            ErrorRatePercent: 2.5,
            P95LatencyMs: 180,
            RequestsPerMinute: 14.8,
            TopEndpoint: "GET /api/service-requests",
            RequestsSeries:
            [
                new AdminMonitoringTimeseriesPointDto(nowUtc.AddMinutes(-30), 330),
                new AdminMonitoringTimeseriesPointDto(nowUtc.AddMinutes(-15), 410),
                new AdminMonitoringTimeseriesPointDto(nowUtc, 284)
            ],
            ErrorsSeries:
            [
                new AdminMonitoringTimeseriesPointDto(nowUtc.AddMinutes(-30), 6),
                new AdminMonitoringTimeseriesPointDto(nowUtc.AddMinutes(-15), 9),
                new AdminMonitoringTimeseriesPointDto(nowUtc, 4)
            ],
            LatencySeries:
            [
                new AdminMonitoringLatencyTimeseriesPointDto(nowUtc.AddMinutes(-30), 80, 180, 230),
                new AdminMonitoringLatencyTimeseriesPointDto(nowUtc.AddMinutes(-15), 82, 188, 240),
                new AdminMonitoringLatencyTimeseriesPointDto(nowUtc, 78, 176, 226)
            ],
            StatusDistribution:
            [
                new AdminMonitoringStatusDistributionDto(200, 970),
                new AdminMonitoringStatusDistributionDto(500, 22)
            ],
            TopErrors:
            [
                new AdminMonitoringTopErrorDto(
                    ErrorKey: "timeout_redis",
                    ErrorType: "TimeoutException",
                    Message: "Timeout redis",
                    Count: 8,
                    EndpointTemplate: "GET /api/admin/dashboard",
                    StatusCode: 500)
            ],
            ApiUptimeSeconds: 3600,
            ApiHealthStatus: "healthy",
            DatabaseHealthStatus: "healthy",
            ClientPortalHealthStatus: "healthy",
            ProviderPortalHealthStatus: "healthy");
    }
}
