using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminGrowthControllerReactivationTests
{
    /// <summary>
    /// Cenario: endpoint do cockpit executivo retorna North Star e KPIs consolidados.
    /// Passos: service mockado devolve payload com metas trimestrais e tendencia semanal.
    /// Resultado esperado: API responde 200 com contrato esperado.
    /// </summary>
    [Fact(DisplayName = "Admin growth controller | Executive cockpit | Deve retornar payload 200")]
    public async Task GetExecutiveCockpit_ShouldReturnOkPayload()
    {
        var growthServiceMock = new Mock<IAdminGrowthService>();
        growthServiceMock
            .Setup(x => x.GetExecutiveCockpitAsync(
                It.IsAny<AdminGrowthExecutiveCockpitQueryDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminGrowthExecutiveCockpitDto(
                FromUtc: DateTime.UtcNow.AddDays(-7),
                ToUtc: DateTime.UtcNow,
                CategoryFilter: null,
                CityFilter: null,
                ProposalSlaMinutes: 30,
                AcceptanceSlaHours: 24,
                NorthStarResolutionHours: 72,
                NorthStarName: "Resolucao Qualificada em ate 72h",
                NorthStarFormula: "RQ72",
                NorthStarRatePercent: 61.25m,
                NorthStarNumerator: 49,
                NorthStarDenominator: 80,
                QuarterTargets: new List<AdminGrowthQuarterTargetDto>
                {
                    new("2026-Q1", 58m, 61.25m, true, "on_track")
                },
                Kpis: new List<AdminGrowthKpiCardDto>
                {
                    new("proposal_coverage", "Cobertura", 82m, "%", "Teste", 75m)
                },
                WeeklyTrend: new List<AdminGrowthWeeklyTrendPointDto>
                {
                    new(DateTime.UtcNow.Date.AddDays(-7), 20, 16, 11, 12, 60m)
                }));

        var liquidityServiceMock = new Mock<IAdminLiquidityScoreService>();
        var controller = new AdminGrowthController(growthServiceMock.Object, liquidityServiceMock.Object);

        var result = await controller.GetExecutiveCockpit(
            fromUtc: DateTime.UtcNow.AddDays(-7),
            toUtc: DateTime.UtcNow,
            category: null,
            city: null,
            proposalSlaMinutes: 30,
            acceptanceSlaHours: 24,
            northStarResolutionHours: 72);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminGrowthExecutiveCockpitDto>(ok.Value);
        Assert.Equal(61.25m, payload.NorthStarRatePercent);
        Assert.Single(payload.QuarterTargets);
    }

    /// <summary>
    /// Cenario: endpoint de ritual semanal retorna agenda e atas recentes.
    /// Passos: service mockado responde snapshot com um item de pauta.
    /// Resultado esperado: API responde 200 com payload do ritual semanal.
    /// </summary>
    [Fact(DisplayName = "Admin growth controller | Weekly ritual | Deve retornar snapshot 200")]
    public async Task GetWeeklyRitual_ShouldReturnOkPayload()
    {
        var growthServiceMock = new Mock<IAdminGrowthService>();
        growthServiceMock
            .Setup(x => x.GetWeeklyRitualSnapshotAsync(
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminGrowthWeeklyRitualSnapshotDto(
                WeekStartUtc: DateTime.UtcNow.Date,
                Agenda: new List<AdminGrowthWeeklyRitualAgendaItemDto>
                {
                    new(1, "North Star", "Growth Operacional", "Avaliar variacao semanal")
                },
                RecentRecords: Array.Empty<AdminGrowthWeeklyRitualRecordDto>()));

        var liquidityServiceMock = new Mock<IAdminLiquidityScoreService>();
        var controller = new AdminGrowthController(growthServiceMock.Object, liquidityServiceMock.Object);

        var result = await controller.GetWeeklyRitual(DateTime.UtcNow);
        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminGrowthWeeklyRitualSnapshotDto>(ok.Value);
        Assert.Single(payload.Agenda);
    }

    /// <summary>
    /// Cenario: endpoint de registro de ata semanal persiste resultado operacional.
    /// Passos: request valido com ator autenticado.
    /// Resultado esperado: API retorna 200 com registro da ata.
    /// </summary>
    [Fact(DisplayName = "Admin growth controller | Weekly ritual record | Deve registrar ata")]
    public async Task RecordWeeklyRitual_ShouldReturnOkPayload()
    {
        var growthServiceMock = new Mock<IAdminGrowthService>();
        growthServiceMock
            .Setup(x => x.RecordWeeklyRitualAsync(
                It.IsAny<AdminGrowthWeeklyRitualRecordRequestDto>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminGrowthWeeklyRitualRecordDto(
                RecordId: Guid.NewGuid(),
                CreatedAtUtc: DateTime.UtcNow,
                ActorEmail: "growth-admin@teste.com",
                Summary: "Resumo",
                Decisions: "Decisoes",
                OwnerActions: "Owners",
                Risks: "Riscos",
                NextActions: "Proximos passos"));

        var liquidityServiceMock = new Mock<IAdminLiquidityScoreService>();
        var controller = new AdminGrowthController(growthServiceMock.Object, liquidityServiceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")),
                        new Claim(ClaimTypes.Email, "growth-admin@teste.com")
                    }, "test-auth"))
                }
            }
        };

        var result = await controller.RecordWeeklyRitual(
            new AdminGrowthWeeklyRitualRecordRequestDto(
                Summary: "Resumo",
                Decisions: "Decisoes",
                OwnerActions: "Owners",
                Risks: "Riscos",
                NextActions: "Proximos passos"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminGrowthWeeklyRitualRecordDto>(ok.Value);
        Assert.Equal("growth-admin@teste.com", payload.ActorEmail);
    }

    /// <summary>
    /// Cenario: endpoint de revisao mensal retorna agenda e historico executivo.
    /// Passos: service mockado responde snapshot de mes com um item de pauta.
    /// Resultado esperado: API responde 200 com payload da revisao mensal.
    /// </summary>
    [Fact(DisplayName = "Admin growth controller | Monthly review | Deve retornar snapshot 200")]
    public async Task GetMonthlyReview_ShouldReturnOkPayload()
    {
        var growthServiceMock = new Mock<IAdminGrowthService>();
        growthServiceMock
            .Setup(x => x.GetMonthlyReviewSnapshotAsync(
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminGrowthMonthlyReviewSnapshotDto(
                MonthStartUtc: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                Agenda: new List<AdminGrowthMonthlyReviewAgendaItemDto>
                {
                    new(1, "Fechamento do mes", "Growth Operacional", "Avaliar resultado mensal")
                },
                RecentRecords: Array.Empty<AdminGrowthMonthlyReviewRecordDto>()));

        var liquidityServiceMock = new Mock<IAdminLiquidityScoreService>();
        var controller = new AdminGrowthController(growthServiceMock.Object, liquidityServiceMock.Object);

        var result = await controller.GetMonthlyReview(DateTime.UtcNow);
        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminGrowthMonthlyReviewSnapshotDto>(ok.Value);
        Assert.Single(payload.Agenda);
    }

    /// <summary>
    /// Cenario: endpoint de registro da revisao mensal persiste ata executiva.
    /// Passos: request valido com ator autenticado e resumo obrigatorio.
    /// Resultado esperado: API retorna 200 com registro do fechamento mensal.
    /// </summary>
    [Fact(DisplayName = "Admin growth controller | Monthly review record | Deve registrar ata executiva")]
    public async Task RecordMonthlyReview_ShouldReturnOkPayload()
    {
        var growthServiceMock = new Mock<IAdminGrowthService>();
        growthServiceMock
            .Setup(x => x.RecordMonthlyReviewAsync(
                It.IsAny<AdminGrowthMonthlyReviewRecordRequestDto>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminGrowthMonthlyReviewRecordDto(
                RecordId: Guid.NewGuid(),
                MonthStartUtc: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedAtUtc: DateTime.UtcNow,
                ActorEmail: "growth-admin@teste.com",
                ExecutiveSummary: "Resumo executivo",
                StrategicDecisions: "Decisoes",
                RisksAndBlockers: "Riscos",
                NextMonthBets: "Bets",
                BudgetNotes: "Budget"));

        var liquidityServiceMock = new Mock<IAdminLiquidityScoreService>();
        var controller = new AdminGrowthController(growthServiceMock.Object, liquidityServiceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")),
                        new Claim(ClaimTypes.Email, "growth-admin@teste.com")
                    }, "test-auth"))
                }
            }
        };

        var result = await controller.RecordMonthlyReview(
            new AdminGrowthMonthlyReviewRecordRequestDto(
                ReferenceMonthUtc: DateTime.UtcNow,
                ExecutiveSummary: "Resumo executivo",
                StrategicDecisions: "Decisoes",
                RisksAndBlockers: "Riscos",
                NextMonthBets: "Bets",
                BudgetNotes: "Budget"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminGrowthMonthlyReviewRecordDto>(ok.Value);
        Assert.Equal("growth-admin@teste.com", payload.ActorEmail);
    }

    /// <summary>
    /// Cenario: endpoint de segmentacao de reativacao devolve snapshot para operacao admin.
    /// Passos: service mockado retorna payload consolidado por segmento.
    /// Resultado esperado: API responde 200 com contrato esperado.
    /// </summary>
    [Fact(DisplayName = "Admin growth controller | Reativacao segmentos | Deve retornar payload 200")]
    public async Task GetProviderReactivationSegments_ShouldReturnOkPayload()
    {
        var growthServiceMock = new Mock<IAdminGrowthService>();
        growthServiceMock
            .Setup(x => x.GetProviderReactivationSegmentsAsync(
                It.IsAny<AdminProviderReactivationSegmentsQueryDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminProviderReactivationSegmentsDto(
                AsOfUtc: DateTime.UtcNow,
                TotalProviders: 20,
                ActiveProviders: 12,
                InactiveProviders: 8,
                Segments: new List<AdminProviderReactivationSegmentBreakdownDto>
                {
                    new("dormant", "Dormente", 31, 60, 5, 62.5m, 3, 2, "Hidraulica", "CEP 20031")
                },
                Preview: new List<AdminProviderReactivationProviderPreviewDto>()));

        var liquidityServiceMock = new Mock<IAdminLiquidityScoreService>();
        var controller = new AdminGrowthController(growthServiceMock.Object, liquidityServiceMock.Object);

        var result = await controller.GetProviderReactivationSegments(null, 7, 15, 31, 61, 50);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminProviderReactivationSegmentsDto>(ok.Value);
        Assert.Equal(8, payload.InactiveProviders);
    }

    /// <summary>
    /// Cenario: admin dispara rodada de campanha com cadencia e segmentacao.
    /// Passos: endpoint recebe payload valido e actor resolvido via JWT.
    /// Resultado esperado: API devolve 200 com resultado de campanha.
    /// </summary>
    [Fact(DisplayName = "Admin growth controller | Reativacao campanha | Deve retornar resultado da rodada")]
    public async Task RunProviderReactivationCampaign_ShouldReturnOkPayload()
    {
        var growthServiceMock = new Mock<IAdminGrowthService>();
        var expected = new AdminProviderReactivationCampaignRunResultDto(
            CampaignId: Guid.NewGuid(),
            RequestedAtUtc: DateTime.UtcNow,
            Executed: true,
            Status: "completed",
            Message: "Rodada concluida.",
            CadenceHours: 24,
            ForceRun: false,
            SelectedProviders: 2,
            SegmentCode: "dormant",
            PreviousCampaignAtUtc: null,
            Recipients: new List<AdminProviderReactivationProviderPreviewDto>());

        growthServiceMock
            .Setup(x => x.RunProviderReactivationCampaignAsync(
                It.IsAny<AdminProviderReactivationCampaignRunRequestDto>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var liquidityServiceMock = new Mock<IAdminLiquidityScoreService>();
        var controller = new AdminGrowthController(growthServiceMock.Object, liquidityServiceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")),
                        new Claim(ClaimTypes.Email, "growth-admin@teste.com")
                    }, "test-auth"))
                }
            }
        };

        var result = await controller.RunProviderReactivationCampaign(new AdminProviderReactivationCampaignRunRequestDto(
            AsOfUtc: DateTime.UtcNow,
            CadenceHours: 24,
            MaxRecipients: 100,
            ForceRun: false,
            SegmentCode: "dormant"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminProviderReactivationCampaignRunResultDto>(ok.Value);
        Assert.True(payload.Executed);
        Assert.Equal("completed", payload.Status);
        growthServiceMock.Verify(x => x.RunProviderReactivationCampaignAsync(
            It.IsAny<AdminProviderReactivationCampaignRunRequestDto>(),
            It.IsAny<Guid>(),
            "growth-admin@teste.com",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Cenario: consulta de performance de campanhas de reativacao para painel admin.
    /// Passos: service retorna consolidado com taxa de reativacao.
    /// Resultado esperado: API responde 200 com payload de performance.
    /// </summary>
    [Fact(DisplayName = "Admin growth controller | Performance campanha | Deve retornar payload 200")]
    public async Task GetProviderReactivationCampaignPerformance_ShouldReturnOkPayload()
    {
        var growthServiceMock = new Mock<IAdminGrowthService>();
        growthServiceMock
            .Setup(x => x.GetProviderReactivationCampaignPerformanceAsync(
                It.IsAny<AdminProviderReactivationCampaignPerformanceQueryDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminProviderReactivationCampaignPerformanceDto(
                FromUtc: DateTime.UtcNow.AddDays(-7),
                ToUtc: DateTime.UtcNow,
                TotalCampaigns: 2,
                TotalSelectedProviders: 10,
                TotalReactivatedProviders: 4,
                ReactivationRatePercent: 40m,
                TotalSystemSent: 10,
                TotalPushSent: 8,
                TotalEmailSent: 2,
                TotalFailed: 1,
                Items: new List<AdminProviderReactivationCampaignPerformanceItemDto>()));

        var liquidityServiceMock = new Mock<IAdminLiquidityScoreService>();
        var controller = new AdminGrowthController(growthServiceMock.Object, liquidityServiceMock.Object);

        var result = await controller.GetProviderReactivationCampaignPerformance(
            fromUtc: DateTime.UtcNow.AddDays(-7),
            toUtc: DateTime.UtcNow,
            take: 20);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminProviderReactivationCampaignPerformanceDto>(ok.Value);
        Assert.Equal(2, payload.TotalCampaigns);
        Assert.Equal(40m, payload.ReactivationRatePercent);
    }

    /// <summary>
    /// Cenario: admin atualiza preferencia de opt-out/frequencia de um prestador.
    /// Passos: endpoint recebe providerId valido e retorna snapshot da preferencia.
    /// Resultado esperado: API responde 200 com dados persistidos.
    /// </summary>
    [Fact(DisplayName = "Admin growth controller | Preferencia reativacao | Deve retornar payload 200")]
    public async Task UpsertProviderReactivationPreference_ShouldReturnOkPayload()
    {
        var growthServiceMock = new Mock<IAdminGrowthService>();
        growthServiceMock
            .Setup(x => x.UpsertProviderReactivationPreferenceAsync(
                It.IsAny<AdminProviderReactivationPreferenceUpsertRequestDto>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminProviderReactivationPreferenceDto(
                ProviderId: Guid.NewGuid(),
                OptOut: true,
                MaxTouchesPerWeek: 2,
                Reason: "Solicitacao do prestador",
                UpdatedAtUtc: DateTime.UtcNow,
                UpdatedByEmail: "growth-admin@teste.com"));

        var liquidityServiceMock = new Mock<IAdminLiquidityScoreService>();
        var controller = new AdminGrowthController(growthServiceMock.Object, liquidityServiceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")),
                        new Claim(ClaimTypes.Email, "growth-admin@teste.com")
                    }, "test-auth"))
                }
            }
        };

        var result = await controller.UpsertProviderReactivationPreference(
            new AdminProviderReactivationPreferenceUpsertRequestDto(
                ProviderId: Guid.NewGuid(),
                OptOut: true,
                MaxTouchesPerWeek: 2,
                Reason: "Solicitacao do prestador"));

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminProviderReactivationPreferenceDto>(ok.Value);
        Assert.True(payload.OptOut);
        Assert.Equal(2, payload.MaxTouchesPerWeek);
    }
}
