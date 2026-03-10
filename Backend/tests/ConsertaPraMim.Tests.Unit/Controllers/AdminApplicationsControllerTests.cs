using ConsertaPraMim.Web.Admin.Controllers;
using ConsertaPraMim.Web.Admin.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Net;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class AdminApplicationsControllerTests
{
    [Fact(DisplayName = "AdminApplications | Deve apontar APK para /files/apks/hml quando DEPLOY_PROFILE=development")]
    public async Task Index_ShouldUseHmlApkDirectory_WhenDeployProfileIsDevelopment()
    {
        var controller = CreateController(
            host: "hml.admin.consertapramim.com",
            scheme: "https",
            settings: new Dictionary<string, string?>
            {
                ["DEPLOY_PROFILE"] = "development",
                ["Fileserver:ApkBaseUrl"] = "http://localhost:8080/files/apks",
                ["ApiBaseUrl"] = "http://localhost:5193",
                ["MobileWebViews:ClientUrl"] = "http://localhost:5181",
                ["MobileWebViews:ProviderUrl"] = "http://localhost:5182",
                ["MobileWebViews:AdminUrl"] = "http://localhost:5183"
            });

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminApplicationsViewModel>(viewResult.Model);
        Assert.Equal("http://hml.admin.consertapramim.com:8080/files/apks/hml", model.FileserverBaseUrl);

        var clientCard = Assert.Single(model.Applications, card => card.AppKind == "client");
        Assert.Equal(
            "http://hml.admin.consertapramim.com:8080/files/apks/hml/ConsertaPraMim-Cliente-compat.apk",
            clientCard.DownloadUrl);
    }

    [Fact(DisplayName = "AdminApplications | Deve apontar APK para /files/apks/prd quando DEPLOY_PROFILE=production")]
    public async Task Index_ShouldUsePrdApkDirectory_WhenDeployProfileIsProduction()
    {
        var controller = CreateController(
            host: "admin.consertapramim.com",
            scheme: "https",
            settings: new Dictionary<string, string?>
            {
                ["DEPLOY_PROFILE"] = "production",
                ["Fileserver:ApkBaseUrl"] = "http://localhost:8080/files/apks/hml",
                ["ApiBaseUrl"] = "http://localhost:5193",
                ["MobileWebViews:ClientUrl"] = "http://localhost:5181",
                ["MobileWebViews:ProviderUrl"] = "http://localhost:5182",
                ["MobileWebViews:AdminUrl"] = "http://localhost:5183"
            });

        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<AdminApplicationsViewModel>(viewResult.Model);
        Assert.Equal("http://admin.consertapramim.com:8080/files/apks/prd", model.FileserverBaseUrl);

        var adminCard = Assert.Single(model.Applications, card => card.AppKind == "admin");
        Assert.Equal(
            "http://admin.consertapramim.com:8080/files/apks/prd/ConsertaPraMim-Admin-compat.apk",
            adminCard.DownloadUrl);
    }

    private static AdminApplicationsController CreateController(
        string host,
        string scheme,
        IReadOnlyDictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(new AlwaysNotFoundHttpMessageHandler())
            {
                Timeout = TimeSpan.FromSeconds(2)
            });

        var controller = new AdminApplicationsController(configuration, httpClientFactoryMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        controller.HttpContext.Request.Host = new HostString(host);
        controller.HttpContext.Request.Scheme = scheme;
        controller.HttpContext.Request.Path = "/AdminApplications";

        return controller;
    }

    private sealed class AlwaysNotFoundHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
