using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminGrowthAiServiceTests
{
    [Fact(DisplayName = "Admin growth AI service | Settings | Deve exigir api key ao habilitar modulo")]
    public async Task UpsertSettingsAsync_ShouldRequireApiKey_WhenEnabled()
    {
        var storeMock = new Mock<IAdminGrowthAiStore>();
        storeMock
            .Setup(x => x.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(AdminGrowthAiStoreSnapshot.Empty);

        var growthServiceMock = new Mock<IAdminGrowthService>();
        var liquidityServiceMock = new Mock<IAdminLiquidityScoreService>();
        var gatewayMock = new Mock<IAdminGrowthAiGateway>();

        var service = new AdminGrowthAiService(
            storeMock.Object,
            growthServiceMock.Object,
            liquidityServiceMock.Object,
            gatewayMock.Object);

        var result = await service.UpsertSettingsAsync(
            new AdminGrowthAiUpsertSettingsRequestDto(
                Enabled: true,
                ApiKey: null,
                Model: "gpt-4.1-mini",
                Temperature: 0.2m,
                MaxOutputTokens: 900,
                SystemPrompt: "prompt"),
            Guid.NewGuid(),
            "admin@teste.com");

        Assert.False(result.Success);
        Assert.Equal("openai_api_key_required", result.ErrorCode);
    }

    [Fact(DisplayName = "Admin growth AI service | Analyze | Deve gerar analise e persistir historico")]
    public async Task AnalyzeAsync_ShouldGenerateAndPersistHistory()
    {
        var savedSnapshots = new List<AdminGrowthAiStoreSnapshot>();
        var initialSnapshot = new AdminGrowthAiStoreSnapshot(
            Settings: new AdminGrowthAiStoreSettings(
                Enabled: true,
                Provider: "OpenAI",
                Model: "gpt-4.1-mini",
                ApiKey: "sk-test",
                Temperature: 0.2m,
                MaxOutputTokens: 900,
                SystemPrompt: "system prompt",
                UpdatedAtUtc: DateTime.UtcNow),
            Analyses: Array.Empty<AdminGrowthAiAnalysisDto>());

        var storeMock = new Mock<IAdminGrowthAiStore>();
        storeMock
            .Setup(x => x.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(initialSnapshot);
        storeMock
            .Setup(x => x.SaveAsync(It.IsAny<AdminGrowthAiStoreSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback<AdminGrowthAiStoreSnapshot, CancellationToken>((snapshot, _) => savedSnapshots.Add(snapshot))
            .Returns(Task.CompletedTask);

        var growthServiceMock = new Mock<IAdminGrowthService>();
        growthServiceMock
            .Setup(x => x.GetFunnelAsync(It.IsAny<AdminGrowthFunnelQueryDto>()))
            .ReturnsAsync(new AdminGrowthFunnelDto(
                FromUtc: DateTime.UtcNow.AddDays(-7),
                ToUtc: DateTime.UtcNow,
                CategoryFilter: "Electrical",
                CityFilter: "Campinas",
                ProposalSlaMinutes: 30,
                AcceptanceSlaMinutes: 24 * 60,
                RequestsTotal: 100,
                RequestsWithAnyProposal: 70,
                RequestsWithoutProposal: 30,
                AcceptedRequests: 35,
                ScheduledOrBeyondRequests: 20,
                CompletedRequests: 15,
                FirstProposalStage: new AdminGrowthFunnelStageDto("first_proposal", 100, 70, 30, 50, 20, 71.4m, 42m, 30m),
                ProposalAcceptanceStage: new AdminGrowthFunnelStageDto("acceptance", 70, 35, 35, 24, 11, 68.57m, 120m, 95m),
                Alerts: Array.Empty<AdminGrowthAlertDto>()));
        growthServiceMock
            .Setup(x => x.GetExecutiveCockpitAsync(
                It.IsAny<AdminGrowthExecutiveCockpitQueryDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminGrowthExecutiveCockpitDto(
                FromUtc: DateTime.UtcNow.AddDays(-7),
                ToUtc: DateTime.UtcNow,
                CategoryFilter: "Electrical",
                CityFilter: "Campinas",
                ProposalSlaMinutes: 30,
                AcceptanceSlaHours: 24,
                NorthStarResolutionHours: 72,
                NorthStarName: "Pedidos concluídos em até 72h",
                NorthStarFormula: "Concluidos_72h / Pedidos_abertos",
                NorthStarRatePercent: 35.5m,
                NorthStarNumerator: 355,
                NorthStarDenominator: 1000,
                QuarterTargets: new List<AdminGrowthQuarterTargetDto>
                {
                    new("2026-Q1", 40m, 35.5m, true, "atencao")
                },
                Kpis: new List<AdminGrowthKpiCardDto>
                {
                    new("proposal_coverage", "Cobertura", 75m, "%", "Cobertura de propostas", 80m)
                },
                WeeklyTrend: new List<AdminGrowthWeeklyTrendPointDto>
                {
                    new(DateTime.UtcNow.AddDays(-7), 100, 70, 35, 20, 35.5m)
                }));
        growthServiceMock
            .Setup(x => x.GetWeeklyRitualSnapshotAsync(
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminGrowthWeeklyRitualSnapshotDto(
                WeekStartUtc: DateTime.UtcNow.Date,
                Agenda: new List<AdminGrowthWeeklyRitualAgendaItemDto>
                {
                    new(1, "Liquidez", "Operacoes", "Elevar cobertura")
                },
                RecentRecords: new List<AdminGrowthWeeklyRitualRecordDto>
                {
                    new(
                        RecordId: Guid.NewGuid(),
                        CreatedAtUtc: DateTime.UtcNow,
                        ActorEmail: "admin@teste.com",
                        Summary: "Resumo semanal",
                        Decisions: "Decisoes",
                        OwnerActions: "Acoes",
                        Risks: "Riscos",
                        NextActions: "Proximos passos")
                }));
        growthServiceMock
            .Setup(x => x.GetMonthlyReviewSnapshotAsync(
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminGrowthMonthlyReviewSnapshotDto(
                MonthStartUtc: new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                Agenda: new List<AdminGrowthMonthlyReviewAgendaItemDto>
                {
                    new(1, "North star", "Growth", "Revisar meta")
                },
                RecentRecords: new List<AdminGrowthMonthlyReviewRecordDto>
                {
                    new(
                        RecordId: Guid.NewGuid(),
                        MonthStartUtc: new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                        CreatedAtUtc: DateTime.UtcNow,
                        ActorEmail: "admin@teste.com",
                        ExecutiveSummary: "Resumo mensal",
                        StrategicDecisions: "Decisoes estrategicas",
                        RisksAndBlockers: "Bloqueios",
                        NextMonthBets: "Apostas",
                        BudgetNotes: "Notas")
                }));

        var liquidityServiceMock = new Mock<IAdminLiquidityScoreService>();
        liquidityServiceMock
            .Setup(x => x.GetScoreAsync(It.IsAny<AdminLiquidityScoreQueryDto>()))
            .ReturnsAsync(new AdminLiquidityScoreResponseDto(
                FromUtc: DateTime.UtcNow.AddDays(-7),
                ToUtc: DateTime.UtcNow,
                CategoryFilter: "Electrical",
                CityFilter: "Campinas",
                ProposalSlaMinutes: 30,
                FormulaDescription: "formula",
                Items: new List<AdminLiquidityScoreItemDto>
                {
                    new("Campinas", "Electrical", 40, 30, 10, 12, 75m, 66m, 20m, 71m, "warning")
                },
                History: Array.Empty<AdminLiquidityScoreHistoryPointDto>(),
                Alerts: Array.Empty<AdminGrowthAlertDto>()));

        var gatewayMock = new Mock<IAdminGrowthAiGateway>();
        gatewayMock
            .Setup(x => x.GenerateAnalysisAsync(It.IsAny<AdminGrowthAiGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminGrowthAiGatewayResult(
                Success: true,
                OutputText: """
                            {
                              "executiveSummary":"Liquidez em risco moderado na categoria eletrica.",
                              "funnelInsights":["Cobertura de propostas abaixo de 75%."],
                              "liquidityInsights":["Regiao Campinas com score warning."],
                              "risks":["Aumento de pedidos sem proposta em 7 dias."],
                              "recommendedActions":["Campanha de reativacao segmentada para eletrica."]
                            }
                            """,
                InputTokens: 1200,
                OutputTokens: 220,
                TotalTokens: 1420));

        var service = new AdminGrowthAiService(
            storeMock.Object,
            growthServiceMock.Object,
            liquidityServiceMock.Object,
            gatewayMock.Object);

        var result = await service.AnalyzeAsync(
            new AdminGrowthAiAnalyzeRequestDto(
                FromUtc: DateTime.UtcNow.AddDays(-7),
                ToUtc: DateTime.UtcNow,
                Category: "Electrical",
                City: "Campinas",
                ProposalSlaMinutes: 30,
                AcceptanceSlaHours: 24,
                LiquidityTake: 10),
            Guid.NewGuid(),
            "admin@teste.com");

        Assert.True(result.Success);
        Assert.NotNull(result.Analysis);
        Assert.Contains("Liquidez", result.Analysis!.ExecutiveSummary);
        Assert.NotEmpty(result.Analysis.RecommendedActions);
        Assert.Single(savedSnapshots);
        Assert.Single(savedSnapshots[0].Analyses);
    }
}
