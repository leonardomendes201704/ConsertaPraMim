using System.Security.Claims;
using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class ClientSupportTicketsControllerTests
{
    /// <summary>
    /// Cenario: cliente autenticado abre a aba de ajuda de um pedido que ja possui atendimento contextual.
    /// Passos: mocka o servico de suporte retornando ticket vinculado ao pedido e executa GET do endpoint dedicado.
    /// Resultado esperado: API responde 200 com o snapshot do atendimento para a UI renderizar o historico.
    /// </summary>
    [Fact(DisplayName = "Client support tickets controller | Get by service request | Deve retornar ok quando ticket existe")]
    public async Task GetByServiceRequest_ShouldReturnOk_WhenTicketExists()
    {
        var clientUserId = Guid.NewGuid();
        var serviceRequestId = Guid.NewGuid();
        var serviceMock = new Mock<IClientSupportTicketService>();
        var details = BuildDetails(serviceRequestId);

        serviceMock
            .Setup(service => service.GetByServiceRequestAsync(clientUserId, serviceRequestId))
            .ReturnsAsync(details);

        var controller = CreateController(serviceMock.Object, clientUserId);
        var result = await controller.GetByServiceRequest(serviceRequestId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(details, ok.Value);
    }

    /// <summary>
    /// Cenario: cliente envia mensagem invalida (sem texto e sem anexo) para a ajuda contextual.
    /// Passos: mocka o servico retornando erro de validacao e executa POST do endpoint de mensagem.
    /// Resultado esperado: API responde BadRequest com codigo de erro coerente para a UI exibir feedback.
    /// </summary>
    [Fact(DisplayName = "Client support tickets controller | Add message | Deve retornar bad request quando servico falha")]
    public async Task AddMessage_ShouldReturnBadRequest_WhenServiceFails()
    {
        var clientUserId = Guid.NewGuid();
        var serviceRequestId = Guid.NewGuid();
        var serviceMock = new Mock<IClientSupportTicketService>();

        serviceMock
            .Setup(service => service.AddMessageAsync(clientUserId, serviceRequestId, It.IsAny<ClientSupportTicketMessageRequestDto>()))
            .ReturnsAsync(new ClientSupportTicketOperationResultDto(
                false,
                ErrorCode: "client_support_message_required",
                ErrorMessage: "Informe uma mensagem ou anexe ao menos um arquivo."));

        var controller = CreateController(serviceMock.Object, clientUserId);
        var result = await controller.AddMessage(serviceRequestId, new ClientSupportTicketMessageRequestDto(string.Empty));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    private static ClientSupportTicketsController CreateController(
        IClientSupportTicketService service,
        Guid clientUserId)
    {
        var controller = new ClientSupportTicketsController(service);
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, clientUserId.ToString()),
                new Claim(ClaimTypes.Role, "Client")
            ], "Test"))
        };

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    private static ClientSupportTicketDetailsDto BuildDetails(Guid serviceRequestId)
    {
        return new ClientSupportTicketDetailsDto(
            new ClientSupportTicketSummaryDto(
                Guid.NewGuid(),
                serviceRequestId,
                "Ajuda sobre o pedido",
                "ClientServiceRequestHelp",
                "Medium",
                "Open",
                DateTime.UtcNow,
                DateTime.UtcNow,
                null,
                null,
                null,
                null,
                1),
            [
                new ClientSupportTicketMessageDto(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "Client",
                    "Cliente",
                    "ClientOpened",
                    "Preciso de ajuda",
                    [],
                    DateTime.UtcNow)
            ]);
    }
}
