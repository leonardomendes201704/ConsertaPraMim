using System.Threading;

namespace AppMobileCPM.Observability;

public static class ChatwootCorrelationContext
{
    private static readonly AsyncLocal<string?> CurrentCorrelationId = new();

    public const string HeaderName = "X-Correlation-ID";
    public const string HttpContextItemKey = "__cpm_correlation_id";

    public static string? Current => CurrentCorrelationId.Value;

    public static IDisposable Push(string? correlationId = null)
    {
        var previous = CurrentCorrelationId.Value;
        CurrentCorrelationId.Value = Sanitize(correlationId) ?? Create("chatwoot");
        return new PopWhenDisposed(previous);
    }

    public static string GetOrCreate(string prefix = "chatwoot")
    {
        if (!string.IsNullOrWhiteSpace(CurrentCorrelationId.Value))
        {
            return CurrentCorrelationId.Value!;
        }

        var correlationId = Create(prefix);
        CurrentCorrelationId.Value = correlationId;
        return correlationId;
    }

    public static string Create(string prefix)
    {
        var normalizedPrefix = Sanitize(prefix) ?? "chatwoot";
        return $"{normalizedPrefix}-{Guid.NewGuid():N}";
    }

    private static string? Sanitize(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Length == 0)
        {
            return null;
        }

        return normalized.Length <= 120 ? normalized : normalized[..120];
    }

    private sealed class PopWhenDisposed : IDisposable
    {
        private readonly string? _previous;
        private bool _disposed;

        public PopWhenDisposed(string? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CurrentCorrelationId.Value = _previous;
        }
    }
}
