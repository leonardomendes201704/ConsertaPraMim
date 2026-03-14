using Microsoft.AspNetCore.Http.Extensions;

namespace AppMobileCPM.Observability;

public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.TraceIdentifier = correlationId;
        context.Items[ChatwootCorrelationContext.HttpContextItemKey] = correlationId;
        context.Response.Headers[ChatwootCorrelationContext.HeaderName] = correlationId;

        using var correlationScope = ChatwootCorrelationContext.Push(correlationId);
        using var loggerScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId
        });

        _logger.LogDebug(
            "Requisicao iniciada. CorrelationId={CorrelationId} Method={Method} Path={Path}",
            correlationId,
            context.Request.Method,
            context.Request.GetDisplayUrl());

        await _next(context);
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(ChatwootCorrelationContext.HeaderName, out var headerValue))
        {
            var candidate = Sanitize(headerValue.ToString());
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        var traceIdentifier = Sanitize(context.TraceIdentifier);
        if (!string.IsNullOrWhiteSpace(traceIdentifier))
        {
            return traceIdentifier;
        }

        return ChatwootCorrelationContext.Create("req");
    }

    private static string Sanitize(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        if (normalized.Length > 120)
        {
            normalized = normalized[..120];
        }

        return normalized;
    }
}
