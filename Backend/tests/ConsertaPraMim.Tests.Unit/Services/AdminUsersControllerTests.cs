using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminUsersControllerTests
{
    [Fact(DisplayName = "Admin usuarios controller | Controller | Deve protected com admin only politica")]
    public void Controller_ShouldBeProtectedWithAdminOnlyPolicy()
    {
        var authorize = typeof(AdminUsersController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("AdminOnly", authorize!.Policy);
    }

    [Fact(DisplayName = "Admin usuarios controller | Obter por id | Deve retornar nao encontrado quando usuario nao exist")]
    public async Task GetById_ShouldReturnNotFound_WhenUserDoesNotExist()
    {
        var serviceMock = new Mock<IAdminUserService>();
        serviceMock.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((AdminUserDetailsDto?)null);
        var controller = new AdminUsersController(serviceMock.Object);

        var result = await controller.GetById(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact(DisplayName = "Admin usuarios controller | Atualizar status | Deve retornar conflito quando servico rejects")]
    public async Task UpdateStatus_ShouldReturnConflict_WhenServiceRejects()
    {
        var userId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var serviceMock = new Mock<IAdminUserService>();
        serviceMock.Setup(s => s.UpdateStatusAsync(
                userId,
                It.IsAny<AdminUpdateUserStatusRequestDto>(),
                actorId,
                "admin@teste.com"))
            .ReturnsAsync(new AdminUpdateUserStatusResultDto(false, "last_admin_forbidden", "erro"));

        var controller = new AdminUsersController(serviceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, actorId.ToString()),
                        new Claim(ClaimTypes.Email, "admin@teste.com")
                    }))
                }
            }
        };

        var result = await controller.UpdateStatus(userId, new AdminUpdateUserStatusRequestDto(false, "x"));

        Assert.IsType<ConflictObjectResult>(result);
    }
}
