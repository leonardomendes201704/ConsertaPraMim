using System.Security.Claims;
using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminGrowthControllerAiTests
{
    [Fact(DisplayName = "Admin growth controller | AI snapshot | Deve retornar payload 200")]
    public async Task GetAiSnapshot_ShouldReturnOkPayload()
    {
        var growthServiceMock = new Mock<IAdminGrowthService>();
        var liquidityServiceMock = new Mock<IAdminLiquidityScoreService>();
        var growthAiServiceMock = new Mock<IAdminGrowthAiService>();
        growthAiServiceMock
            .Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminGrowthAiSnapshotDto(
                new AdminGrowthAiSettingsDto(
                    Enabled: true,
                    IsConfigured: true,
                    Provider: "OpenAI",
                    Model: "gpt-4.1-mini",
                    Temperature: 0.2m,
                    MaxOutputTokens: 900,
                    SystemPrompt: "prompt",
                    ApiKeyMasked: "sk-...1234",
                    UpdatedAtUtc: DateTime.UtcNow,
                    LastAnalysisAtUtc: DateTime.UtcNow),
                RecentAnalyses: Array.Empty<AdminGrowthAiAnalysisDto>()));

        var controller = new AdminGrowthController(
            growthServiceMock.Object,
            liquidityServiceMock.Object,
            growthAiServiceMock.Object);

        var result = await controller.GetAiSnapshot();
        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminGrowthAiSnapshotDto>(ok.Value);
        Assert.True(payload.Settings.IsConfigured);
    }

    [Fact(DisplayName = "Admin growth controller | AI analyze | Deve retornar bad request quando servico falha")]
    public async Task AnalyzeWithAi_ShouldReturnBadRequest_WhenServiceFails()
    {
        var growthServiceMock = new Mock<IAdminGrowthService>();
        var liquidityServiceMock = new Mock<IAdminLiquidityScoreService>();
        var growthAiServiceMock = new Mock<IAdminGrowthAiService>();
        growthAiServiceMock
            .Setup(x => x.AnalyzeAsync(
                It.IsAny<AdminGrowthAiAnalyzeRequestDto>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminGrowthAiAnalyzeResultDto(
                Success: false,
                ErrorCode: "growth_ai_not_configured",
                ErrorMessage: "Copiloto IA nao configurado."));

        var controller = new AdminGrowthController(
            growthServiceMock.Object,
            liquidityServiceMock.Object,
            growthAiServiceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")),
                        new Claim(ClaimTypes.Email, "admin@teste.com")
                    }, "test-auth"))
                }
            }
        };

        var result = await controller.AnalyzeWithAi(
            new AdminGrowthAiAnalyzeRequestDto(
                FromUtc: DateTime.UtcNow.AddDays(-7),
                ToUtc: DateTime.UtcNow,
                Category: null,
                City: null));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var payload = Assert.IsType<AdminGrowthAiAnalyzeResultDto>(badRequest.Value);
        Assert.False(payload.Success);
        Assert.Equal("growth_ai_not_configured", payload.ErrorCode);
    }
}
