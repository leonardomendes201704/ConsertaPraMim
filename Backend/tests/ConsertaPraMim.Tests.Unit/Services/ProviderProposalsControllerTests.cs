using ConsertaPraMim.Web.Provider.Controllers;
using ConsertaPraMim.Web.Provider.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class ProviderProposalsControllerTests
{
    /// <summary>
    /// Cenario: a API devolve erro tecnico de validacao ao tentar enviar a proposta.
    /// Passos: mocka SubmitProposalAsync com falha e a mensagem interna "Escopo da proposta e obrigatorio.".
    /// Resultado esperado: o controller converte a mensagem para um texto objetivo ao prestador e redireciona ao detalhe do pedido.
    /// </summary>
    [Fact(DisplayName = "Prestador proposals controller | Submit | Deve normalizar erro tecnico da proposta")]
    public async Task Submit_ShouldNormalizeValidationError_WhenApiReturnsTechnicalMessage()
    {
        var backendApiClientMock = new Mock<IProviderBackendApiClient>();
        var requestId = Guid.NewGuid();

        backendApiClientMock
            .Setup(client => client.SubmitProposalAsync(
                It.IsAny<ConsertaPraMim.Application.DTOs.CreateProposalDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, "Escopo da proposta e obrigatorio."));

        var controller = CreateController(backendApiClientMock.Object);

        var result = await controller.Submit(
            requestId,
            estimatedValue: null,
            estimatedLeadTimeHours: null,
            warrantyDays: null,
            message: null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal("ServiceRequests", redirect.ControllerName);
        Assert.Equal(requestId, redirect.RouteValues!["id"]);
        Assert.Equal("Informe a mensagem ao cliente para enviar a proposta.", controller.TempData["Error"]);
    }

    private static ProposalsController CreateController(IProviderBackendApiClient backendApiClient)
    {
        var httpContext = new DefaultHttpContext();

        return new ProposalsController(backendApiClient)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            TempData = new TempDataDictionary(
                httpContext,
                Mock.Of<ITempDataProvider>())
        };
    }
}
