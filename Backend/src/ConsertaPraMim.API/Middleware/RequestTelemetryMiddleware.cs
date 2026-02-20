using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

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

    private static readonly HashSet<string> SensitiveHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Set-Cookie",
        "X-Api-Key",
        "X-Auth-Token",
        "Proxy-Authorization",
        "Cf-Access-Jwt-Assertion",
        "Sec-WebSocket-Key"
    };

    private static readonly string[] SensitiveKeyFragments =
    [
        "password",
        "passwd",
        "pwd",
        "token",
        "secret",
        "apikey",
        "api_key",
        "auth",
        "jwt"
    ];

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTelemetryMiddleware> _logger;
    private readonly IRequestTelemetryBuffer _telemetryBuffer;
    private readonly IMonitoringRuntimeSettings _monitoringRuntimeSettings;
    private readonly bool _enabledByConfiguration;
    private readonly bool _captureSwagger;
    private readonly bool _captureRequestBody;
    private readonly bool _captureResponseBody;
    private readonly bool _captureHeaders;
    private readonly bool _captureQueryString;
    private readonly bool _captureRouteValues;
    private readonly int _maxBodyChars;
    private readonly int _maxContextChars;
    private readonly string _ipHashSalt;

    public RequestTelemetryMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<RequestTelemetryMiddleware> logger,
        IRequestTelemetryBuffer telemetryBuffer,
        IMonitoringRuntimeSettings monitoringRuntimeSettings)
    {
        _next = next;
        _logger = logger;
        _telemetryBuffer = telemetryBuffer;
        _monitoringRuntimeSettings = monitoringRuntimeSettings;
        _enabledByConfiguration = ParseBool(configuration["Monitoring:Enabled"], defaultValue: true);
        _captureSwagger = ParseBool(configuration["Monitoring:CaptureSwaggerRequests"], defaultValue: false);
        _captureRequestBody = ParseBool(configuration["Monitoring:BodyCapture:CaptureRequestBody"], defaultValue: false);
        _captureResponseBody = ParseBool(configuration["Monitoring:BodyCapture:CaptureResponseBody"], defaultValue: false);
        _maxBodyChars = ParseInt(configuration["Monitoring:BodyCapture:MaxBodyChars"], defaultValue: 4000, minValue: 256, maxValue: 64000);
        _captureHeaders = ParseBool(configuration["Monitoring:ContextCapture:CaptureHeaders"], defaultValue: true);
        _captureQueryString = ParseBool(configuration["Monitoring:ContextCapture:CaptureQueryString"], defaultValue: true);
        _captureRouteValues = ParseBool(configuration["Monitoring:ContextCapture:CaptureRouteValues"], defaultValue: true);
        _maxContextChars = ParseInt(configuration["Monitoring:ContextCapture:MaxContextChars"], defaultValue: 8000, minValue: 256, maxValue: 64000);
        _ipHashSalt = string.IsNullOrWhiteSpace(configuration["Monitoring:IpHashSalt"])
            ? "cpm-monitoring-salt"
            : configuration["Monitoring:IpHashSalt"]!;
    }

    public async Task InvokeAsync(HttpContext context, IRequestWarningCollector warningCollector)
    {
        if (!_enabledByConfiguration || ShouldSkipPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        if (!await _monitoringRuntimeSettings.IsTelemetryEnabledAsync(context.RequestAborted))
        {
            await _next(context);
            return;
        }

        warningCollector.Clear();
        var startedAtUtc = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        Exception? exception = null;
        var requestBodyJson = await TryCaptureRequestBodyAsync(context.Request, context.RequestAborted);
        string? responseBodyJson = null;

        LimitedBufferingWriteStream? responseCapture = null;
        Stream? originalResponseBodyStream = null;
        if (_captureResponseBody)
        {
            originalResponseBodyStream = context.Response.Body;
            responseCapture = new LimitedBufferingWriteStream(
                originalResponseBodyStream,
                Math.Clamp(_maxBodyChars * 4, 1024, 256000));
            context.Response.Body = responseCapture;
        }

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
            if (responseCapture != null && originalResponseBodyStream != null)
            {
                try
                {
                    await context.Response.Body.FlushAsync(context.RequestAborted);
                }
                catch
                {
                    // no-op
                }

                context.Response.Body = originalResponseBodyStream;
                responseBodyJson = TryCaptureResponseBody(context.Response.ContentType, responseCapture);
            }

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
            var requestHeadersJson = _captureHeaders ? TryCaptureHeadersJson(context.Request.Headers) : null;
            var queryStringJson = _captureQueryString ? TryCaptureQueryStringJson(context.Request.Query) : null;
            var routeValuesJson = _captureRouteValues ? TryCaptureRouteValuesJson(context.Request.RouteValues) : null;

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
                Host: context.Request.Host.Value,
                RequestBodyJson: requestBodyJson,
                ResponseBodyJson: responseBodyJson,
                RequestHeadersJson: requestHeadersJson,
                QueryStringJson: queryStringJson,
                RouteValuesJson: routeValuesJson);

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

    private async Task<string?> TryCaptureRequestBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!_captureRequestBody || !CanMethodHaveBody(request.Method))
        {
            return null;
        }

        var hasKnownTextContentType = ShouldCaptureBodyForContentType(request.ContentType);
        var hasUnknownContentType = !string.IsNullOrWhiteSpace(request.ContentType) && !hasKnownTextContentType;

        if (!request.Body.CanRead)
        {
            return null;
        }

        // Some clients/proxies send request body using chunked transfer without Content-Length.
        // In this case, ContentLength is null and we still want to capture payload.
        if (request.ContentLength.HasValue && request.ContentLength.Value == 0)
        {
            return null;
        }

        try
        {
            request.EnableBuffering();
            if (request.Body.CanSeek)
            {
                request.Body.Position = 0;
            }

            var (rawBody, truncated) = await ReadLimitedTextAsync(request.Body, _maxBodyChars, cancellationToken);

            if (request.Body.CanSeek)
            {
                request.Body.Position = 0;
            }

            var normalized = NormalizeCapturedBody(rawBody, truncated);
            if (normalized == null)
            {
                return null;
            }

            if (hasUnknownContentType && LooksLikeBinaryPayload(normalized))
            {
                return JsonSerializer.Serialize(new
                {
                    message = "Payload nao textual omitido na telemetria.",
                    contentType = request.ContentType,
                    contentLength = request.ContentLength
                });
            }

            return normalized;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falha ao capturar request body para telemetria. Path={Path}", request.Path.Value);
            if (request.Body.CanSeek)
            {
                request.Body.Position = 0;
            }

            return null;
        }
    }

    private string? TryCaptureResponseBody(string? contentType, LimitedBufferingWriteStream capture)
    {
        var capturedBytes = capture.GetCapturedBytes();
        if (capturedBytes.Length == 0)
        {
            return null;
        }

        // Capture only textual/json payloads to avoid storing binary response buffers.
        if (!string.IsNullOrWhiteSpace(contentType) && !ShouldCaptureBodyForContentType(contentType))
        {
            return null;
        }

        try
        {
            var rawBody = Encoding.UTF8.GetString(capturedBytes);
            return NormalizeCapturedBody(rawBody, capture.WasTruncated);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falha ao converter response body capturado para UTF-8.");
            return null;
        }
    }

    private string? TryCaptureHeadersJson(IHeaderDictionary headers)
    {
        if (headers.Count == 0)
        {
            return null;
        }

        var captured = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var key = header.Key?.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var values = CaptureStringValues(header.Value, isSensitive: IsSensitiveHeader(key));
            if (values == null)
            {
                continue;
            }

            captured[key] = values.Length == 1 ? values[0] : values;
        }

        return SerializeCapturedContext(captured);
    }

    private string? TryCaptureQueryStringJson(IQueryCollection query)
    {
        if (query.Count == 0)
        {
            return null;
        }

        var captured = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var key = pair.Key?.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var values = CaptureStringValues(pair.Value, isSensitive: IsSensitiveKey(key));
            if (values == null)
            {
                continue;
            }

            captured[key] = values.Length == 1 ? values[0] : values;
        }

        return SerializeCapturedContext(captured);
    }

    private string? TryCaptureRouteValuesJson(RouteValueDictionary routeValues)
    {
        if (routeValues.Count == 0)
        {
            return null;
        }

        var captured = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in routeValues.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var key = pair.Key?.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var raw = pair.Value?.ToString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            captured[key] = IsSensitiveKey(key)
                ? "***REDACTED***"
                : LimitString(raw.Trim(), _maxContextChars);
        }

        return SerializeCapturedContext(captured);
    }

    private string? SerializeCapturedContext(IDictionary<string, object?> captured)
    {
        if (captured.Count == 0)
        {
            return null;
        }

        var serialized = JsonSerializer.Serialize(captured);
        if (serialized.Length <= _maxContextChars)
        {
            return serialized;
        }

        return JsonSerializer.Serialize(new
        {
            truncated = true,
            originalLength = serialized.Length,
            maxChars = _maxContextChars,
            preview = serialized[.._maxContextChars]
        });
    }

    private string[]? CaptureStringValues(StringValues values, bool isSensitive)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var captured = new List<string>(capacity: values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            var raw = values[i];
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            captured.Add(isSensitive
                ? "***REDACTED***"
                : LimitString(raw.Trim(), _maxContextChars));
        }

        return captured.Count == 0 ? null : captured.ToArray();
    }

    private static bool IsSensitiveHeader(string headerName)
    {
        return SensitiveHeaderNames.Contains(headerName);
    }

    private static bool IsSensitiveKey(string key)
    {
        var normalized = key.Trim().ToLowerInvariant();
        for (var i = 0; i < SensitiveKeyFragments.Length; i++)
        {
            if (normalized.Contains(SensitiveKeyFragments[i], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string LimitString(string value, int maxChars)
    {
        if (value.Length <= maxChars)
        {
            return value;
        }

        return $"{value[..maxChars]}\n/* [TRUNCATED] */";
    }

    private static async Task<(string? Body, bool Truncated)> ReadLimitedTextAsync(
        Stream stream,
        int maxChars,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024,
            leaveOpen: true);

        var builder = new StringBuilder(capacity: Math.Min(maxChars, 4096));
        var buffer = new char[1024];
        var totalRead = 0;
        var truncated = false;

        while (true)
        {
            var maxCharsToRead = Math.Min(buffer.Length, (maxChars + 1) - totalRead);
            if (maxCharsToRead <= 0)
            {
                truncated = true;
                break;
            }

            var read = await reader.ReadAsync(buffer.AsMemory(0, maxCharsToRead), cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
            if (totalRead <= maxChars)
            {
                builder.Append(buffer, 0, read);
            }
            else
            {
                var allowed = read - (totalRead - maxChars);
                if (allowed > 0)
                {
                    builder.Append(buffer, 0, allowed);
                }

                truncated = true;
                break;
            }
        }

        if (builder.Length == 0)
        {
            return (null, truncated);
        }

        return (builder.ToString(), truncated);
    }

    private string? NormalizeCapturedBody(string? rawBody, bool truncated)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return null;
        }

        var normalized = rawBody.Length <= _maxBodyChars
            ? rawBody
            : rawBody[.._maxBodyChars];

        return truncated || rawBody.Length > _maxBodyChars
            ? $"{normalized}\n/* [TRUNCATED] */"
            : normalized;
    }

    private static bool ShouldCaptureBodyForContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var normalized = contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
        return normalized.StartsWith("application/json", StringComparison.Ordinal) ||
               normalized.EndsWith("+json", StringComparison.Ordinal) ||
               normalized.StartsWith("text/", StringComparison.Ordinal) ||
               normalized == "application/xml" ||
               normalized == "text/xml" ||
               normalized == "application/x-www-form-urlencoded";
    }

    private static bool LooksLikeBinaryPayload(string payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return false;
        }

        var sampleLength = Math.Min(payload.Length, 1024);
        var controlChars = 0;
        for (var i = 0; i < sampleLength; i++)
        {
            var ch = payload[i];
            if (!char.IsControl(ch))
            {
                continue;
            }

            if (ch is '\r' or '\n' or '\t')
            {
                continue;
            }

            controlChars++;
        }

        // Heuristica simples para evitar persistir lixo binario no campo textual.
        return controlChars > (sampleLength * 0.20);
    }

    private static bool CanMethodHaveBody(string? method)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            return false;
        }

        var normalized = method.Trim().ToUpperInvariant();
        return normalized is not ("GET" or "HEAD" or "OPTIONS" or "TRACE");
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

    private static int ParseInt(string? raw, int defaultValue, int minValue, int maxValue)
    {
        if (!int.TryParse(raw, out var parsed))
        {
            return defaultValue;
        }

        return Math.Clamp(parsed, minValue, maxValue);
    }

    private static bool IsError(int statusCode, Exception? exception)
    {
        return exception != null || statusCode >= 500;
    }

    private sealed class LimitedBufferingWriteStream : Stream
    {
        private readonly Stream _inner;
        private readonly MemoryStream _captured;
        private readonly int _maxCaptureBytes;
        private int _capturedBytes;

        public LimitedBufferingWriteStream(Stream inner, int maxCaptureBytes)
        {
            _inner = inner;
            _maxCaptureBytes = Math.Max(1, maxCaptureBytes);
            _captured = new MemoryStream(capacity: Math.Min(_maxCaptureBytes, 8192));
        }

        public bool WasTruncated { get; private set; }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => _inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner.Write(buffer, offset, count);
            Capture(new ReadOnlySpan<byte>(buffer, offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _inner.Write(buffer);
            Capture(buffer);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Capture(new ReadOnlySpan<byte>(buffer, offset, count));
            return _inner.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Capture(buffer.Span);
            return _inner.WriteAsync(buffer, cancellationToken);
        }

        public byte[] GetCapturedBytes()
        {
            return _captured.ToArray();
        }

        private void Capture(ReadOnlySpan<byte> buffer)
        {
            var remaining = _maxCaptureBytes - _capturedBytes;
            if (remaining <= 0)
            {
                if (buffer.Length > 0)
                {
                    WasTruncated = true;
                }

                return;
            }

            var lengthToCopy = Math.Min(remaining, buffer.Length);
            if (lengthToCopy > 0)
            {
                _captured.Write(buffer[..lengthToCopy]);
                _capturedBytes += lengthToCopy;
            }

            if (buffer.Length > lengthToCopy)
            {
                WasTruncated = true;
            }
        }
    }
}
