using Microsoft.Extensions.Primitives;

namespace ConsertaPraMim.API.Middleware;

/// <summary>
/// Garante correlation id consistente para cada request HTTP.
/// Aceita `X-Correlation-ID` recebido do cliente; quando ausente, gera novo identificador.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string CorrelationIdHeaderName = "X-Correlation-ID";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(
        RequestDelegate next,
        ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context.Request.Headers);

        context.TraceIdentifier = correlationId;
        context.Items[CorrelationIdHeaderName] = correlationId;
        context.Response.Headers[CorrelationIdHeaderName] = correlationId;

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(IHeaderDictionary headers)
    {
        if (headers.TryGetValue(CorrelationIdHeaderName, out StringValues values))
        {
            var provided = values.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(provided) && provided.Length <= 128)
            {
                return provided;
            }
        }

        return Guid.NewGuid().ToString("N");
    }
}
