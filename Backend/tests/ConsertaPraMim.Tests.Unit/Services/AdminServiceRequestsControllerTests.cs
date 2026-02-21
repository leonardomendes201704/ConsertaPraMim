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

    [Fact(DisplayName = "Admin servico requisicoes controller | Obter por id | Deve retornar nao encontrado quando requisicao nao exist")]
    public async Task GetById_ShouldReturnNotFound_WhenRequestDoesNotExist()
    {
        var serviceMock = new Mock<IAdminRequestProposalService>();
        serviceMock.Setup(s => s.GetServiceRequestByIdAsync(It.IsAny<Guid>())).ReturnsAsync((AdminServiceRequestDetailsDto?)null);
        var controller = new AdminServiceRequestsController(serviceMock.Object);

        var result = await controller.GetById(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

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
