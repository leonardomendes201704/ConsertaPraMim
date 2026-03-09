using ConsertaPraMim.Web.Landing.Controllers;
using ConsertaPraMim.Web.Landing.Models;
using ConsertaPraMim.Web.Landing.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class LandingHomeControllerTests
{
    [Fact(DisplayName = "Landing | HomeController | Index deve renderizar a home sem origem inicial")]
    public async Task Index_ShouldRenderLandingWithoutInitialLeadOrigin()
    {
        var notificationClientMock = new Mock<ILandingAdminNotificationsClient>();
        var controller = CreateController(notificationClientMock);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LandingPageViewModel>(view.Model);
        Assert.Equal("ConsertaPraMim \u2013 Encontre profissionais de confianca", controller.ViewData["Title"]);
        Assert.Equal("https://www.consertapramim.com/", controller.ViewData["CanonicalUrl"]);
        Assert.Equal("https://www.consertapramim.com/", controller.ViewData["OpenGraphUrl"]);
        Assert.Equal("https://www.consertapramim.com/og-logo-consertapramim.png", controller.ViewData["OpenGraphImage"]);
        Assert.Equal("https://api.consertapramim.com", model.ApiBaseUrl);
        Assert.Equal("https://api.consertapramim.com/api/landing-leads/public", model.LeadCaptureUrl);
        Assert.Equal("https://api.consertapramim.com/swagger", model.ApiSwaggerUrl);
        Assert.Equal("https://cliente.consertapramim.com/", model.ClientPortalUrl);
        Assert.False(string.IsNullOrWhiteSpace(model.VisitorId));
        Assert.Equal(model.VisitorId, controller.ViewData["LandingVisitorId"]);
        Assert.Null(model.InitialLeadOrigin);
        Assert.Contains("cpm_landing_vid=", controller.HttpContext.Response.Headers.SetCookie.ToString(), StringComparison.Ordinal);

        notificationClientMock.Verify(client => client.NotifyLandingAccessAsync(
            It.Is<LandingAccessNotificationRequest>(request =>
                request.VisitorId == model.VisitorId &&
                request.Path == "/" &&
                request.InitialLeadOrigin == null &&
                request.Host == "www.consertapramim.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Landing | HomeController | Cliente deve abrir o fluxo direto do lead de cliente")]
    public async Task Cliente_ShouldRenderLandingWithClientLeadOrigin()
    {
        var notificationClientMock = new Mock<ILandingAdminNotificationsClient>();
        var controller = CreateController(notificationClientMock, "/Cliente");

        var result = await controller.Cliente();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LandingPageViewModel>(view.Model);
        Assert.Equal("client", model.InitialLeadOrigin);
        Assert.False(string.IsNullOrWhiteSpace(model.VisitorId));
        Assert.Equal("client", controller.ViewData["InitialLeadOrigin"]);
        Assert.Equal("https://www.consertapramim.com/Cliente", controller.ViewData["OpenGraphUrl"]);
        Assert.Equal("https://www.consertapramim.com/og-logo-consertapramim.png", controller.ViewData["OpenGraphImage"]);
        Assert.Equal("https://api.consertapramim.com", model.ApiBaseUrl);
        Assert.Equal("https://api.consertapramim.com/api/landing-leads/public", model.LeadCaptureUrl);

        notificationClientMock.Verify(client => client.NotifyLandingAccessAsync(
            It.Is<LandingAccessNotificationRequest>(request =>
                request.VisitorId == model.VisitorId &&
                request.Path == "/Cliente" &&
                request.InitialLeadOrigin == "client"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Landing | HomeController | Prestador deve abrir o fluxo direto do lead de prestador")]
    public async Task Prestador_ShouldRenderLandingWithProviderLeadOrigin()
    {
        var notificationClientMock = new Mock<ILandingAdminNotificationsClient>();
        var controller = CreateController(notificationClientMock, "/Prestador");

        var result = await controller.Prestador();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LandingPageViewModel>(view.Model);
        Assert.Equal("provider", model.InitialLeadOrigin);
        Assert.False(string.IsNullOrWhiteSpace(model.VisitorId));
        Assert.Equal("provider", controller.ViewData["InitialLeadOrigin"]);
        Assert.Equal("https://www.consertapramim.com/Prestador", controller.ViewData["OpenGraphUrl"]);
        Assert.Equal("https://www.consertapramim.com/og-logo-consertapramim.png", controller.ViewData["OpenGraphImage"]);
        Assert.Equal("https://api.consertapramim.com/api/landing-leads/public", model.LeadCaptureUrl);
        Assert.Equal("https://prestador.consertapramim.com/", model.ProviderPortalUrl);

        notificationClientMock.Verify(client => client.NotifyLandingAccessAsync(
            It.Is<LandingAccessNotificationRequest>(request =>
                request.VisitorId == model.VisitorId &&
                request.Path == "/Prestador" &&
                request.InitialLeadOrigin == "provider"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static HomeController CreateController(
        Mock<ILandingAdminNotificationsClient> notificationClientMock,
        string path = "/")
    {
        var options = Options.Create(new LandingSiteOptions
        {
            CanonicalUrl = "https://www.consertapramim.com",
            ClientPortalUrl = "http://187.77.48.150:5069",
            ProviderPortalUrl = "http://187.77.48.150:5140",
            AdminPortalUrl = "http://187.77.48.150:5151",
            ApiBaseUrl = "http://187.77.48.150:5193",
            ApiSwaggerUrl = "http://187.77.48.150:5193/swagger",
            InternalApiBaseUrl = "http://cpm-api:8080",
            InternalWebhookToken = "segredo-interno"
        });

        var controller = new HomeController(
            options,
            notificationClientMock.Object,
            NullLogger<HomeController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        controller.HttpContext.Request.Host = new HostString("www.consertapramim.com");
        controller.HttpContext.Request.Scheme = "https";
        controller.HttpContext.Request.Path = path;

        return controller;
    }
}
