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
}
