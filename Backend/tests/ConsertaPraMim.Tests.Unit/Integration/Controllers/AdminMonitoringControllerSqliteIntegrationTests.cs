using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Infrastructure.Services;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConsertaPraMim.Tests.Unit.Integration.Controllers;

public class AdminMonitoringControllerSqliteIntegrationTests
{
    [Fact]
    public void Controller_ShouldBeProtectedWithAdminOnlyPolicy()
    {
        var authorize = typeof(AdminMonitoringController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("AdminOnly", authorize!.Policy);
    }

    [Fact]
    public async Task Endpoints_ShouldReturnData_FromPersistedTelemetry()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var service = new AdminMonitoringService(context, NullLogger<AdminMonitoringService>.Instance);
            var controller = new AdminMonitoringController(service)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };
            var now = DateTime.UtcNow;

            var events = new[]
            {
                CreateEvent(now.AddMinutes(-15), "corr-a", "GET", "/api/mobile-client/orders", 200, 110, "info", false),
                CreateEvent(now.AddMinutes(-8), "corr-b", "GET", "/api/mobile-client/orders", 200, 180, "warn", false, warningCount: 1),
                CreateEvent(now.AddMinutes(-3), "corr-c", "POST", "/api/mobile-client/orders", 500, 390, "error", true, "InvalidOperationException", "erro de persistencia")
            };

            await service.SaveRawEventsAsync(events);

            var overviewResult = await controller.GetOverview("24h");
            var overviewOk = Assert.IsType<OkObjectResult>(overviewResult);
            var overview = Assert.IsType<AdminMonitoringOverviewDto>(overviewOk.Value);
            Assert.Equal(3, overview.TotalRequests);
            Assert.True(overview.ErrorRatePercent > 0);
            Assert.NotEmpty(overview.StatusDistribution);

            var topResult = await controller.GetTopEndpoints("24h", take: 10);
            var topOk = Assert.IsType<OkObjectResult>(topResult);
            var top = Assert.IsType<AdminMonitoringTopEndpointsResponseDto>(topOk.Value);
            Assert.NotEmpty(top.Items);
            Assert.Equal("/api/mobile-client/orders", top.Items[0].EndpointTemplate);

            var requestsResult = await controller.GetRequests("24h", page: 1, pageSize: 20);
            var requestsOk = Assert.IsType<OkObjectResult>(requestsResult);
            var requests = Assert.IsType<AdminMonitoringRequestsResponseDto>(requestsOk.Value);
            Assert.Equal(3, requests.Total);
            Assert.Equal(3, requests.Items.Count);

            var detailsResult = await controller.GetRequestByCorrelationId("corr-c");
            var detailsOk = Assert.IsType<OkObjectResult>(detailsResult);
            var details = Assert.IsType<AdminMonitoringRequestDetailsDto>(detailsOk.Value);
            Assert.Equal("corr-c", details.CorrelationId);
            Assert.Equal(500, details.StatusCode);
            Assert.Equal("error", details.Severity);
        }
    }

    private static ApiRequestTelemetryEventDto CreateEvent(
        DateTime timestampUtc,
        string correlationId,
        string method,
        string endpointTemplate,
        int statusCode,
        int durationMs,
        string severity,
        bool isError,
        string? errorType = null,
        string? normalizedErrorMessage = null,
        int warningCount = 0)
    {
        var normalizedErrorKey = string.IsNullOrWhiteSpace(errorType)
            ? null
            : $"{errorType?.ToLowerInvariant()}|{statusCode}";

        return new ApiRequestTelemetryEventDto(
            TimestampUtc: timestampUtc,
            CorrelationId: correlationId,
            TraceId: $"{correlationId}-trace",
            Method: method,
            EndpointTemplate: endpointTemplate,
            Path: endpointTemplate,
            StatusCode: statusCode,
            DurationMs: durationMs,
            Severity: severity,
            IsError: isError,
            WarningCount: warningCount,
            WarningCodesJson: warningCount > 0 ? "[\"validation_error\"]" : null,
            ErrorType: errorType,
            NormalizedErrorMessage: normalizedErrorMessage,
            NormalizedErrorKey: normalizedErrorKey,
            IpHash: "iphash-test",
            UserAgent: "integration-tests",
            UserId: null,
            TenantId: null,
            RequestSizeBytes: 128,
            ResponseSizeBytes: 512,
            Scheme: "https",
            Host: "localhost");
    }
}
