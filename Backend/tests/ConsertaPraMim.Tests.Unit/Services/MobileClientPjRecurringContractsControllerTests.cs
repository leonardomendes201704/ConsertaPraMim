using System.Security.Claims;
using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class MobileClientPjRecurringContractsControllerTests
{
    /// <summary>
    /// Cenario: app cliente tenta listar contratos sem claim valida de usuario.
    /// Passos: controller executado com contexto sem NameIdentifier.
    /// Resultado esperado: endpoint retorna 401 com erro de identidade.
    /// </summary>
    [Fact(DisplayName = "Mobile client PJ recurring controller | Listar contratos | Deve retornar unauthorized sem claim de usuario")]
    public async Task List_ShouldReturnUnauthorized_WhenClaimIsMissing()
    {
        var serviceMock = new Mock<IPjRecurringContractService>();
        var controller = new MobileClientPjRecurringContractsController(serviceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.List();

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    /// <summary>
    /// Cenario: cliente envia payload invalido para contratacao PJ.
    /// Passos: service devolve InvalidOperationException e controller processa erro.
    /// Resultado esperado: HTTP 400 BadRequest com codigo funcional.
    /// </summary>
    [Fact(DisplayName = "Mobile client PJ recurring controller | Criar contrato | Deve retornar bad request para operacao invalida")]
    public async Task Create_ShouldReturnBadRequest_WhenServiceThrowsInvalidOperation()
    {
        var clientId = Guid.NewGuid();
        var serviceMock = new Mock<IPjRecurringContractService>();
        serviceMock
            .Setup(x => x.CreateAsync(
                clientId,
                It.IsAny<CreatePjRecurringContractRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("payload invalido"));

        var controller = new MobileClientPjRecurringContractsController(serviceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = BuildHttpContext(clientId)
            }
        };

        var result = await controller.Create(new CreatePjRecurringContractRequestDto(
            ClientPjType.Empresa,
            ServiceCategory.Electrical,
            ProviderClientPreference.Both,
            "Pacote eletrico",
            null,
            PjRecurringCadence.Monthly,
            300m,
            1,
            12,
            480,
            1080,
            62,
            DateTime.UtcNow,
            true));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Cenario: cliente tenta renovar contrato inexistente.
    /// Passos: service lanca KeyNotFoundException no fluxo de renew.
    /// Resultado esperado: endpoint responde 404 para contrato nao encontrado.
    /// </summary>
    [Fact(DisplayName = "Mobile client PJ recurring controller | Renovar contrato | Deve retornar not found quando contrato nao existe")]
    public async Task Renew_ShouldReturnNotFound_WhenServiceThrowsKeyNotFound()
    {
        var clientId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        var serviceMock = new Mock<IPjRecurringContractService>();
        serviceMock
            .Setup(x => x.RenewAsync(
                clientId,
                contractId,
                It.IsAny<RenewPjRecurringContractRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("nao encontrado"));

        var controller = new MobileClientPjRecurringContractsController(serviceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = BuildHttpContext(clientId)
            }
        };

        var result = await controller.Renew(contractId, new RenewPjRecurringContractRequestDto(DateTime.UtcNow, "renew"));

        Assert.IsType<NotFoundObjectResult>(result);
    }

    private static DefaultHttpContext BuildHttpContext(Guid clientId)
    {
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, clientId.ToString()),
                new Claim(ClaimTypes.Role, "Client")
            }))
        };
    }
}
