using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminProposalsControllerTests
{
    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin proposals controller | Controller | Deve protected com admin only politica.
    /// </summary>
    [Fact(DisplayName = "Admin proposals controller | Controller | Deve protected com admin only politica")]
    public void Controller_ShouldBeProtectedWithAdminOnlyPolicy()
    {
        var authorize = typeof(AdminProposalsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("AdminOnly", authorize!.Policy);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin proposals controller | Invalidate | Deve retornar nao autorizado quando claims missing.
    /// </summary>
    [Fact(DisplayName = "Admin proposals controller | Invalidate | Deve retornar nao autorizado quando claims missing")]
    public async Task Invalidate_ShouldReturnUnauthorized_WhenClaimsMissing()
    {
        var serviceMock = new Mock<IAdminRequestProposalService>();
        var controller = new AdminProposalsController(serviceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.Invalidate(Guid.NewGuid(), new AdminInvalidateProposalRequestDto("x"));

        Assert.IsType<UnauthorizedResult>(result);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin proposals controller | Invalidate | Deve retornar nao encontrado quando servico returns nao encontrado.
    /// </summary>
    [Fact(DisplayName = "Admin proposals controller | Invalidate | Deve retornar nao encontrado quando servico returns nao encontrado")]
    public async Task Invalidate_ShouldReturnNotFound_WhenServiceReturnsNotFound()
    {
        var actorUserId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var serviceMock = new Mock<IAdminRequestProposalService>();
        serviceMock.Setup(s => s.InvalidateProposalAsync(
                targetId,
                It.IsAny<AdminInvalidateProposalRequestDto>(),
                actorUserId,
                "admin@teste.com"))
            .ReturnsAsync(new AdminOperationResultDto(false, "not_found", "erro"));

        var controller = new AdminProposalsController(serviceMock.Object)
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

        var result = await controller.Invalidate(targetId, new AdminInvalidateProposalRequestDto("x"));

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
