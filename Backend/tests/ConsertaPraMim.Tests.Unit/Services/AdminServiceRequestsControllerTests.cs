using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminServiceRequestsControllerTests
{
    /// <summary>
    /// Cenario: validacao de seguranca no controller administrativo de requisicoes.
    /// Passos: inspeciona atributos de autorizacao aplicados na classe AdminServiceRequestsController.
    /// Resultado esperado: policy AdminOnly obrigatoria para impedir acesso por perfis nao administrativos.
    /// </summary>
    [Fact(DisplayName = "Admin servico requisicoes controller | Controller | Deve protected com admin only politica")]
    public void Controller_ShouldBeProtectedWithAdminOnlyPolicy()
    {
        var authorize = typeof(AdminServiceRequestsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("AdminOnly", authorize!.Policy);
    }

    /// <summary>
    /// Cenario: admin consulta detalhe de requisicao inexistente.
    /// Passos: servico de dominio retorna nulo para o id informado e o endpoint finaliza a chamada.
    /// Resultado esperado: resposta NotFound, sinalizando ausencia de registro sem erro interno.
    /// </summary>
    [Fact(DisplayName = "Admin servico requisicoes controller | Obter por id | Deve retornar nao encontrado quando requisicao nao exist")]
    public async Task GetById_ShouldReturnNotFound_WhenRequestDoesNotExist()
    {
        var serviceMock = new Mock<IAdminRequestProposalService>();
        serviceMock.Setup(s => s.GetServiceRequestByIdAsync(It.IsAny<Guid>())).ReturnsAsync((AdminServiceRequestDetailsDto?)null);
        var controller = new AdminServiceRequestsController(serviceMock.Object);

        var result = await controller.GetById(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    /// <summary>
    /// Cenario: tentativa de alterar status sem identificar usuario ator no token.
    /// Passos: endpoint UpdateStatus eh chamado com HttpContext sem NameIdentifier.
    /// Resultado esperado: retorno Unauthorized para bloquear mudanca sem identidade auditavel.
    /// </summary>
    [Fact(DisplayName = "Admin servico requisicoes controller | Atualizar status | Deve retornar nao autorizado quando claim missing")]
    public async Task UpdateStatus_ShouldReturnUnauthorized_WhenClaimIsMissing()
    {
        var serviceMock = new Mock<IAdminRequestProposalService>();
        var controller = new AdminServiceRequestsController(serviceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.UpdateStatus(Guid.NewGuid(), new AdminUpdateServiceRequestStatusRequestDto("Created", null));

        Assert.IsType<UnauthorizedResult>(result);
    }

    /// <summary>
    /// Cenario: admin solicita transicao de status invalida para a requisicao.
    /// Passos: camada de servico devolve falha funcional "invalid_status" e controller trata o resultado.
    /// Resultado esperado: retorno BadRequest com erro de negocio, sem mascarar a invalidade da transicao.
    /// </summary>
    [Fact(DisplayName = "Admin servico requisicoes controller | Atualizar status | Deve retornar invalida requisicao quando servico rejects status")]
    public async Task UpdateStatus_ShouldReturnBadRequest_WhenServiceRejectsStatus()
    {
        var actorUserId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var serviceMock = new Mock<IAdminRequestProposalService>();
        serviceMock.Setup(s => s.UpdateServiceRequestStatusAsync(
                targetId,
                It.IsAny<AdminUpdateServiceRequestStatusRequestDto>(),
                actorUserId,
                "admin@teste.com"))
            .ReturnsAsync(new AdminOperationResultDto(false, "invalid_status", "erro"));

        var controller = new AdminServiceRequestsController(serviceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, actorUserId.ToString()),
                        new Claim(ClaimTypes.Email, "admin@teste.com")
                    }))
                }
            }
        };

        var result = await controller.UpdateStatus(targetId, new AdminUpdateServiceRequestStatusRequestDto("Invalid", null));

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
