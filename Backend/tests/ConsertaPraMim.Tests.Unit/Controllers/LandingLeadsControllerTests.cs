using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class LandingLeadsControllerTests
{
    /// <summary>
    /// Cenario: landing publica envia lead anonimo para a API.
    /// Passos: controller recebe o payload, monta o contexto tecnico do HttpContext e delega para o service.
    /// Resultado esperado: `200 OK` com o DTO de confirmacao retornado pelo service.
    /// </summary>
    [Fact(DisplayName = "Landing leads controller | Captura | Deve retornar ok com confirmacao do lead")]
    public async Task CapturePublicLead_ShouldReturnOk()
    {
        var serviceMock = new Mock<ILandingLeadService>();
        var request = new CaptureLandingLeadRequestDto(
            LandingLeadOrigin.Client,
            "Leonardo Silva",
            "13999999999",
            "leo@exemplo.com",
            "Praia Grande",
            "SP",
            "Ocian",
            "Hidraulica",
            "Troca de registro",
            null,
            null,
            null,
            "Preciso de atendimento rapido.",
            "https://www.consertapramim.com/#captacao",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            "pt-BR",
            "1920x1080",
            "Windows",
            "America/Sao_Paulo");

        serviceMock
            .Setup(service => service.CaptureAsync(
                request,
                It.IsAny<LandingLeadCaptureContextDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureLandingLeadResponseDto(
                Guid.NewGuid(),
                LandingLeadOrigin.Client,
                "Recebemos seu interesse.",
                DateTime.UtcNow));

        var controller = new LandingLeadsController(serviceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        controller.ControllerContext.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("187.77.48.150");
        controller.ControllerContext.HttpContext.Request.Host = new HostString("api.consertapramim.com");
        controller.ControllerContext.HttpContext.Request.Scheme = "https";
        controller.ControllerContext.HttpContext.Request.Path = "/api/landing-leads/public";
        controller.ControllerContext.HttpContext.Request.Headers.Referer = "https://www.consertapramim.com/";

        var result = await controller.CapturePublicLead(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        serviceMock.VerifyAll();
    }
}
