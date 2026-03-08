using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class InternalLandingNotificationsControllerTests
{
    [Fact(DisplayName = "Internal landing notifications controller | Auth | Deve rejeitar token invalido")]
    public async Task NotifyAccess_ShouldReturnUnauthorized_WhenTokenIsInvalid()
    {
        var controller = CreateController("segredo-correto", Mock.Of<ILandingAdminNotificationService>());
        controller.ControllerContext.HttpContext.Request.Headers["X-Deploy-Token"] = "token-errado";

        var result = await controller.NotifyAccess(
            new NotifyLandingAccessRequestDto(null, "/", "www.consertapramim.com", "https", null, "187.77.48.150", null, "Mozilla/5.0", "pt-BR", null),
            CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact(DisplayName = "Internal landing notifications controller | Acesso | Deve encaminhar evento valido")]
    public async Task NotifyAccess_ShouldReturnOk_WhenTokenIsValid()
    {
        var notificationServiceMock = new Mock<ILandingAdminNotificationService>();
        var controller = CreateController("segredo-correto", notificationServiceMock.Object);
        controller.ControllerContext.HttpContext.Request.Headers["X-Deploy-Token"] = "segredo-correto";
        var request = new NotifyLandingAccessRequestDto(
            CurrentUrl: "https://www.consertapramim.com/Cliente",
            Path: "/Cliente",
            Host: "www.consertapramim.com",
            Scheme: "https",
            InitialLeadOrigin: "client",
            IpAddress: "187.77.48.150",
            ForwardedFor: "187.77.48.150",
            UserAgent: "Mozilla/5.0",
            AcceptLanguage: "pt-BR",
            RefererUrl: "https://www.google.com/");

        var result = await controller.NotifyAccess(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        notificationServiceMock.Verify(service => service.NotifyLandingAccessAsync(
            It.Is<NotifyLandingAccessRequestDto>(dto => dto.Path == "/Cliente" && dto.InitialLeadOrigin == "client"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static InternalLandingNotificationsController CreateController(
        string? webhookToken,
        ILandingAdminNotificationService landingAdminNotificationService)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DeployNotifications:WebhookToken"] = webhookToken
            })
            .Build();

        return new InternalLandingNotificationsController(
            configuration,
            landingAdminNotificationService,
            NullLogger<InternalLandingNotificationsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }
}
