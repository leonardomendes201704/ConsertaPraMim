using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Routing;

namespace ConsertaPraMim.API.Middleware;

public class RequestTelemetryMiddleware
{
    private static readonly Meter TelemetryMeter = new("ConsertaPraMim.ApiMonitoring", "1.0.0");
    private static readonly Counter<long> RequestCounter = TelemetryMeter.CreateCounter<long>("cpm.api.requests.total");
    private static readonly Counter<long> ErrorCounter = TelemetryMeter.CreateCounter<long>("cpm.api.requests.errors");
    private static readonly Histogram<double> LatencyHistogram = TelemetryMeter.CreateHistogram<double>("cpm.api.request.duration.ms");

    private static readonly Regex GuidRegex = new(
        @"\b[0-9a-fA-F]{8}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{12}\b",
        RegexOptions.Compiled);

    private static readonly Regex NumberRegex = new(
        @"\b\d+\b",
        RegexOptions.Compiled);

    private static readonly Regex EmailRegex = new(
        @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTelemetryMiddleware> _logger;
    private readonly IRequestTelemetryBuffer _telemetryBuffer;
    private readonly bool _enabled;
    private readonly bool _captureSwagger;
    private readonly string _ipHashSalt;

    public RequestTelemetryMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<RequestTelemetryMiddleware> logger,
        IRequestTelemetryBuffer telemetryBuffer)
    {
        _next = next;
        _logger = logger;
        _telemetryBuffer = telemetryBuffer;
        _enabled = ParseBool(configuration["Monitoring:Enabled"], defaultValue: true);
        _captureSwagger = ParseBool(configuration["Monitoring:CaptureSwaggerRequests"], defaultValue: false);
        _ipHashSalt = string.IsNullOrWhiteSpace(configuration["Monitoring:IpHashSalt"])
            ? "cpm-monitoring-salt"
            : configuration["Monitoring:IpHashSalt"]!;
    }

    public async Task InvokeAsync(HttpContext context, IRequestWarningCollector warningCollector)
    {
        if (!_enabled || ShouldSkipPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        warningCollector.Clear();
        var startedAtUtc = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        Exception? exception = null;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            stopwatch.Stop();

            var statusCode = exception != null
                ? StatusCodes.Status500InternalServerError
                : context.Response.StatusCode;

            var warnings = warningCollector.GetWarnings();
            var severity = ResolveSeverity(statusCode, warnings.Count, exception);
            var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
            var correlationId = context.TraceIdentifier;
            var endpointTemplate = ResolveEndpointTemplate(context);
            var normalizedErrorMessage = exception == null ? null : NormalizeErrorMessage(exception.Message);
            var normalizedErrorKey = exception == null
                ? null
                : BuildErrorKey(exception.GetType().Name, normalizedErrorMessage ?? "erro_desconhecido");

            var telemetryEvent = new ApiRequestTelemetryEventDto(
                TimestampUtc: startedAtUtc,
                CorrelationId: correlationId,
                TraceId: traceId,
                Method: context.Request.Method,
                EndpointTemplate: endpointTemplate,
                Path: context.Request.Path.Value ?? "/",
                StatusCode: statusCode,
                DurationMs: (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds),
                Severity: severity,
                IsError: exception != null,
                WarningCount: warnings.Count,
                WarningCodesJson: warnings.Count == 0 ? null : JsonSerializer.Serialize(warnings),
                ErrorType: exception?.GetType().Name,
                NormalizedErrorMessage: normalizedErrorMessage,
                NormalizedErrorKey: normalizedErrorKey,
                IpHash: HashIp(context.Connection.RemoteIpAddress?.ToString()),
                UserAgent: NormalizeSize(context.Request.Headers.UserAgent.ToString(), 512),
                UserId: ResolveUserId(context.User),
                TenantId: ResolveTenantId(context.User),
                RequestSizeBytes: context.Request.ContentLength,
                ResponseSizeBytes: context.Response.ContentLength,
                Scheme: context.Request.Scheme,
                Host: context.Request.Host.Value);

            if (!_telemetryBuffer.TryEnqueue(telemetryEvent))
            {
                _logger.LogWarning(
                    "Telemetry buffer full. Event dropped. Path={Path} Method={Method} Status={StatusCode} QueueLength={QueueLength}",
                    context.Request.Path,
                    context.Request.Method,
                    statusCode,
                    _telemetryBuffer.ApproximateQueueLength);
            }

            var tags = new TagList
            {
                { "method", context.Request.Method },
                { "endpoint", endpointTemplate },
                { "status_code", statusCode.ToString() },
                { "severity", severity }
            };

            RequestCounter.Add(1, tags);
            if (IsError(statusCode, exception))
            {
                ErrorCounter.Add(1, tags);
            }

            LatencyHistogram.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
            warningCollector.Clear();
        }
    }

    private bool ShouldSkipPath(PathString path)
    {
        if (_captureSwagger)
        {
            return false;
        }

        return path.Value?.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string ResolveEndpointTemplate(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint is RouteEndpoint routeEndpoint && !string.IsNullOrWhiteSpace(routeEndpoint.RoutePattern.RawText))
        {
            return routeEndpoint.RoutePattern.RawText!;
        }

        return context.Request.Path.Value ?? "/";
    }

    private static Guid? ResolveUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(raw, out var userId) ? userId : null;
    }

    private static string? ResolveTenantId(ClaimsPrincipal user)
    {
        var tenant = user.FindFirst("tenantId")?.Value
            ?? user.FindFirst("tenant_id")?.Value
            ?? user.FindFirst("tenant")?.Value;

        return string.IsNullOrWhiteSpace(tenant)
            ? null
            : tenant.Trim();
    }

    private static string ResolveSeverity(int statusCode, int warningCount, Exception? exception)
    {
        if (exception != null || statusCode >= 500)
        {
            return "error";
        }

        if (warningCount > 0 || statusCode >= 400)
        {
            return "warn";
        }

        return "info";
    }

    private string? HashIp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var payload = $"{_ipHashSalt}:{value.Trim()}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes)[..32];
    }

    private static string? NormalizeErrorMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var normalized = message.Trim().ToLowerInvariant();
        normalized = GuidRegex.Replace(normalized, "{guid}");
        normalized = EmailRegex.Replace(normalized, "{email}");
        normalized = NumberRegex.Replace(normalized, "{n}");
        return NormalizeSize(normalized, 1200);
    }

    private static string BuildErrorKey(string type, string normalizedMessage)
    {
        var payload = $"{type}|{normalizedMessage}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes)[..40];
    }

    private static string? NormalizeSize(string? input, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var value = input.Trim();
        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }

    private static bool ParseBool(string? raw, bool defaultValue)
    {
        return bool.TryParse(raw, out var parsed)
            ? parsed
            : defaultValue;
    }

    private static bool IsError(int statusCode, Exception? exception)
    {
        return exception != null || statusCode >= 500;
    }
}
