using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class NotificationsControllerTests
{
    [Fact(DisplayName = "Notificacoes controller | Enviar | Deve retornar nao autorizado quando internal api key header missing")]
    public async Task Send_ShouldReturnUnauthorized_WhenInternalApiKeyHeaderIsMissing()
    {
        var notificationServiceMock = new Mock<INotificationService>();
        var controller = CreateController(notificationServiceMock.Object, "internal-key");

        var result = await controller.Send(new NotificationsController.NotificationRequest(
            "recipient",
            "subject",
            "message"));

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact(DisplayName = "Notificacoes controller | Enviar | Deve retornar invalida requisicao quando action url invalido")]
    public async Task Send_ShouldReturnBadRequest_WhenActionUrlIsInvalid()
    {
        var notificationServiceMock = new Mock<INotificationService>();
        var controller = CreateController(notificationServiceMock.Object, "internal-key");
        controller.Request.Headers["X-Internal-Api-Key"] = "internal-key";

        var result = await controller.Send(new NotificationsController.NotificationRequest(
            "recipient",
            "subject",
            "message",
            "https://evil-site.com/phishing"));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact(DisplayName = "Notificacoes controller | Enviar | Deve retornar invalida requisicao quando recipient vazio")]
    public async Task Send_ShouldReturnBadRequest_WhenRecipientIsEmpty()
    {
        var notificationServiceMock = new Mock<INotificationService>();
        var controller = CreateController(notificationServiceMock.Object, "internal-key");
        controller.Request.Headers["X-Internal-Api-Key"] = "internal-key";

        var result = await controller.Send(new NotificationsController.NotificationRequest(
            string.Empty,
            "subject",
            "message"));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact(DisplayName = "Notificacoes controller | Enviar | Deve call notificacao servico quando requisicao valido")]
    public async Task Send_ShouldCallNotificationService_WhenRequestIsValid()
    {
        var notificationServiceMock = new Mock<INotificationService>();
        var controller = CreateController(notificationServiceMock.Object, "internal-key");
        controller.Request.Headers["X-Internal-Api-Key"] = "internal-key";

        var result = await controller.Send(new NotificationsController.NotificationRequest(
            "recipient",
            "subject",
            "message",
            " /ServiceRequests/Details/123 "));

        Assert.IsType<OkResult>(result);
        notificationServiceMock.Verify(s => s.SendNotificationAsync(
            "recipient",
            "subject",
            "message",
            "/ServiceRequests/Details/123"), Times.Once);
    }

    [Fact(DisplayName = "Notificacoes controller | Enviar | Deve use jwt secret fallback quando internal api key nao configured")]
    public async Task Send_ShouldUseJwtSecretFallback_WhenInternalApiKeyIsNotConfigured()
    {
        var notificationServiceMock = new Mock<INotificationService>();
        var controller = CreateController(notificationServiceMock.Object, null, "jwt-secret-key");
        controller.Request.Headers["X-Internal-Api-Key"] = "jwt-secret-key";

        var result = await controller.Send(new NotificationsController.NotificationRequest(
            "recipient",
            "subject",
            "message"));

        Assert.IsType<OkResult>(result);
        notificationServiceMock.Verify(s => s.SendNotificationAsync(
            "recipient",
            "subject",
            "message",
            null), Times.Once);
    }

    private static NotificationsController CreateController(
        INotificationService notificationService,
        string? internalApiKey = null,
        string? jwtSecret = null)
    {
        var values = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(internalApiKey))
        {
            values["InternalNotifications:ApiKey"] = internalApiKey;
        }

        if (!string.IsNullOrWhiteSpace(jwtSecret))
        {
            values["JwtSettings:SecretKey"] = jwtSecret;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var controller = new NotificationsController(notificationService, configuration)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }
}
