using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;

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
}
