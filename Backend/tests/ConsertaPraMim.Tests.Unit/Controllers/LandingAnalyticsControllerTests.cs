using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class LandingAnalyticsControllerTests
{
    [Fact(DisplayName = "Landing analytics controller | Config | Deve retornar config publica")]
    public async Task GetPublicConfig_ShouldReturnOk()
    {
        var runtimeSettingsMock = new Mock<ILandingAnalyticsRuntimeSettings>();
        var telemetryServiceMock = new Mock<ILandingTelemetryEventService>();
        var expected = new LandingAnalyticsPublicConfigDto
        {
            Enabled = true,
            Heartbeat = new LandingHeartbeatRuntimeConfigDto { IntervalSeconds = 15 }
        };

        runtimeSettingsMock
            .Setup(service => service.GetPublicConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = new LandingAnalyticsController(runtimeSettingsMock.Object, telemetryServiceMock.Object);

        var result = await controller.GetPublicConfig(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
    }

    [Fact(DisplayName = "Landing analytics controller | Eventos | Deve encaminhar contexto tecnico ao service")]
    public async Task RecordPublicEvents_ShouldPassRequestContextToService()
    {
        var runtimeSettingsMock = new Mock<ILandingAnalyticsRuntimeSettings>();
        var telemetryServiceMock = new Mock<ILandingTelemetryEventService>();
        var request = new RecordLandingTelemetryBatchRequestDto(
            VisitorId: "visitor-001",
            SessionId: "session-001",
            CurrentUrl: "https://www.consertapramim.com/Cliente",
            Path: "/Cliente",
            Host: "www.consertapramim.com",
            Scheme: "https",
            InitialLeadOrigin: "client",
            ViewportWidth: 1280,
            ViewportHeight: 720,
            BrowserLanguage: "pt-BR",
            Events: []);

        telemetryServiceMock
            .Setup(service => service.RecordBatchAsync(
                request,
                It.Is<LandingLeadCaptureContextDto>(context =>
                    context.IpAddress == "187.77.48.150" &&
                    context.Host == "api.consertapramim.com" &&
                    context.Path == "/api/landing-analytics/public/events"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecordLandingTelemetryBatchResponseDto(0, DateTime.UtcNow));

        var controller = new LandingAnalyticsController(runtimeSettingsMock.Object, telemetryServiceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        controller.ControllerContext.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("187.77.48.150");
        controller.ControllerContext.HttpContext.Request.Host = new HostString("api.consertapramim.com");
        controller.ControllerContext.HttpContext.Request.Scheme = "https";
        controller.ControllerContext.HttpContext.Request.Path = "/api/landing-analytics/public/events";

        var result = await controller.RecordPublicEvents(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        telemetryServiceMock.VerifyAll();
    }
}
