using ConsertaPraMim.Web.Landing.Controllers;
using ConsertaPraMim.Web.Landing.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class LandingHomeControllerTests
{
    [Fact(DisplayName = "Landing | HomeController | Index deve renderizar a home sem origem inicial")]
    public void Index_ShouldRenderLandingWithoutInitialLeadOrigin()
    {
        var controller = CreateController();

        var result = controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LandingPageViewModel>(view.Model);
        Assert.Equal("ConsertaPraMim – Encontre profissionais de confiança", controller.ViewData["Title"]);
        Assert.Equal("https://www.consertapramim.com/", controller.ViewData["CanonicalUrl"]);
        Assert.Equal("https://www.consertapramim.com/", controller.ViewData["OpenGraphUrl"]);
        Assert.Equal("https://www.consertapramim.com/og-logo-consertapramim.png", controller.ViewData["OpenGraphImage"]);
        Assert.Equal("https://api.consertapramim.com", model.ApiBaseUrl);
        Assert.Equal("https://api.consertapramim.com/api/landing-leads/public", model.LeadCaptureUrl);
        Assert.Equal("https://api.consertapramim.com/swagger", model.ApiSwaggerUrl);
        Assert.Equal("https://cliente.consertapramim.com/", model.ClientPortalUrl);
        Assert.Null(model.InitialLeadOrigin);
    }

    [Fact(DisplayName = "Landing | HomeController | Cliente deve abrir o fluxo direto do lead de cliente")]
    public void Cliente_ShouldRenderLandingWithClientLeadOrigin()
    {
        var controller = CreateController();

        var result = controller.Cliente();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LandingPageViewModel>(view.Model);
        Assert.Equal("client", model.InitialLeadOrigin);
        Assert.Equal("client", controller.ViewData["InitialLeadOrigin"]);
        Assert.Equal("https://www.consertapramim.com/Cliente", controller.ViewData["OpenGraphUrl"]);
        Assert.Equal("https://www.consertapramim.com/og-logo-consertapramim.png", controller.ViewData["OpenGraphImage"]);
        Assert.Equal("https://api.consertapramim.com", model.ApiBaseUrl);
        Assert.Equal("https://api.consertapramim.com/api/landing-leads/public", model.LeadCaptureUrl);
    }

    [Fact(DisplayName = "Landing | HomeController | Prestador deve abrir o fluxo direto do lead de prestador")]
    public void Prestador_ShouldRenderLandingWithProviderLeadOrigin()
    {
        var controller = CreateController();

        var result = controller.Prestador();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LandingPageViewModel>(view.Model);
        Assert.Equal("provider", model.InitialLeadOrigin);
        Assert.Equal("provider", controller.ViewData["InitialLeadOrigin"]);
        Assert.Equal("https://www.consertapramim.com/Prestador", controller.ViewData["OpenGraphUrl"]);
        Assert.Equal("https://www.consertapramim.com/og-logo-consertapramim.png", controller.ViewData["OpenGraphImage"]);
        Assert.Equal("https://api.consertapramim.com/api/landing-leads/public", model.LeadCaptureUrl);
        Assert.Equal("https://prestador.consertapramim.com/", model.ProviderPortalUrl);
    }

    private static HomeController CreateController()
    {
        var options = Options.Create(new LandingSiteOptions
        {
            CanonicalUrl = "https://www.consertapramim.com",
            ClientPortalUrl = "http://187.77.48.150:5069",
            ProviderPortalUrl = "http://187.77.48.150:5140",
            AdminPortalUrl = "http://187.77.48.150:5151",
            ApiBaseUrl = "http://187.77.48.150:5193",
            ApiSwaggerUrl = "http://187.77.48.150:5193/swagger"
        });

        var controller = new HomeController(options)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        controller.HttpContext.Request.Host = new HostString("www.consertapramim.com");
        controller.HttpContext.Request.Scheme = "https";

        return controller;
    }
}
