using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminNotificationsControllerTests
{
    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin notificacoes controller | Controller | Deve protected com admin only politica.
    /// </summary>
    [Fact(DisplayName = "Admin notificacoes controller | Controller | Deve protected com admin only politica")]
    public void Controller_ShouldBeProtectedWithAdminOnlyPolicy()
    {
        var authorize = typeof(AdminNotificationsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("AdminOnly", authorize!.Policy);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin notificacoes controller | Enviar | Deve retornar nao autorizado quando claim missing.
    /// </summary>
    [Fact(DisplayName = "Admin notificacoes controller | Enviar | Deve retornar nao autorizado quando claim missing")]
    public async Task Send_ShouldReturnUnauthorized_WhenClaimIsMissing()
    {
        var serviceMock = new Mock<IAdminChatNotificationService>();
        var controller = new AdminNotificationsController(serviceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.Send(new AdminSendNotificationRequestDto(Guid.NewGuid(), "a", "b", null, null));

        Assert.IsType<UnauthorizedResult>(result);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin notificacoes controller | Enviar | Deve retornar invalida requisicao quando servico rejects payload.
    /// </summary>
    [Fact(DisplayName = "Admin notificacoes controller | Enviar | Deve retornar invalida requisicao quando servico rejects payload")]
    public async Task Send_ShouldReturnBadRequest_WhenServiceRejectsPayload()
    {
        var actorUserId = Guid.NewGuid();
        var serviceMock = new Mock<IAdminChatNotificationService>();
        serviceMock.Setup(s => s.SendNotificationAsync(
                It.IsAny<AdminSendNotificationRequestDto>(),
                actorUserId,
                "admin@teste.com"))
            .ReturnsAsync(new AdminSendNotificationResultDto(false, "invalid_payload", "erro"));

        var controller = new AdminNotificationsController(serviceMock.Object)
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

        var result = await controller.Send(new AdminSendNotificationRequestDto(Guid.NewGuid(), string.Empty, string.Empty, null, null));

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
