using System.Security.Claims;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Controllers;
using ConsertaPraMim.Web.Admin.Models;
using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminGrowthAiWebControllerTests
{
    /// <summary>
    /// Cenario: admin abre a pagina AI Copilot Growth sem informar recorte de data.
    /// Passos: executa Index sem from/to e sem token para retornar a view imediatamente.
    /// Resultado esperado: formulario de analise vem preenchido com janela padrao da ultima semana.
    /// </summary>
    [Fact(DisplayName = "Admin growth ai web controller | Index | Deve preencher periodo da ultima semana quando datas nao forem informadas")]
    public async Task Index_ShouldDefaultToLastWeek_WhenRangeIsMissing()
    {
        var apiClientMock = new Mock<IAdminOperationsApiClient>(MockBehavior.Strict);
        var controller = new AdminGrowthAiController(apiClientMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            },
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var result = await controller.Index(fromUtc: null, toUtc: null, category: null, city: null);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminGrowthAiViewModel>(view.Model);

        Assert.NotNull(model.AnalyzeForm.FromUtc);
        Assert.NotNull(model.AnalyzeForm.ToUtc);
        var span = model.AnalyzeForm.ToUtc!.Value - model.AnalyzeForm.FromUtc!.Value;
        Assert.InRange(span.TotalDays, 6.99, 7.01);
    }

    /// <summary>
    /// Cenario: admin dispara analise sem preencher datas no formulario.
    /// Passos: autentica contexto com token admin, executa RunAnalysis e captura payload enviado ao API client.
    /// Resultado esperado: request para API deve carregar janela default de sete dias.
    /// </summary>
    [Fact(DisplayName = "Admin growth ai web controller | Run analysis | Deve enviar periodo da ultima semana quando datas nao forem informadas")]
    public async Task RunAnalysis_ShouldSendLastWeekRange_WhenRangeIsMissing()
    {
        var apiClientMock = new Mock<IAdminOperationsApiClient>();
        AdminGrowthAiAnalyzeRequestDto? capturedRequest = null;

        apiClientMock
            .Setup(client => client.AnalyzeGrowthWithAiAsync(
                It.IsAny<AdminGrowthAiAnalyzeRequestDto>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<AdminGrowthAiAnalyzeRequestDto, string, CancellationToken>((request, _, _) => capturedRequest = request)
            .ReturnsAsync(AdminApiResult<AdminGrowthAiAnalyzeResultDto>.Ok(new AdminGrowthAiAnalyzeResultDto(Success: true)));

        var controller = new AdminGrowthAiController(apiClientMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(AdminClaimTypes.ApiToken, "token-admin-teste")
                    }, "test-auth"))
                }
            },
            TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>())
        };

        var result = await controller.RunAnalysis(new AdminGrowthAiAnalyzeFormModel
        {
            FromUtc = null,
            ToUtc = null
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedRequest!.FromUtc);
        Assert.NotNull(capturedRequest.ToUtc);
        var span = capturedRequest.ToUtc!.Value - capturedRequest.FromUtc!.Value;
        Assert.InRange(span.TotalDays, 6.99, 7.01);
    }
}
