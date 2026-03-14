namespace AppMobileCPM.Integrations.Telegram;

public sealed record TelegramBridgeObservabilitySnapshotDto(
    DateTime GeneratedAtUtc,
    string Environment,
    TelegramBridgeTrafficMetricsDto Traffic,
    TelegramBridgeAiMetricsDto Ai,
    TelegramBridgeBusinessMetricsDto Business,
    IReadOnlyList<TelegramBridgeDependencyMetricsDto> Dependencies,
    IReadOnlyList<TelegramBridgeErrorMetricsDto> TopErrors,
    IReadOnlyList<TelegramBridgeRecentIncidentDto> RecentIncidents);

public sealed record TelegramBridgeTrafficMetricsDto(
    long InboundMessages,
    long OutboundMessages,
    long MessagesWithAttachments);

public sealed record TelegramBridgeAiMetricsDto(
    long Requests,
    long Fallbacks,
    long Failures,
    long GuardrailInterventions,
    long HumanHandoffs,
    double AvgLatencyMs,
    double P95LatencyMs,
    long Tokens,
    decimal EstimatedCostUsd);

public sealed record TelegramBridgeBusinessMetricsDto(
    long TriageRequestsOpened,
    long SchedulingAttempts,
    long SchedulingConfirmed,
    long SchedulingFailures,
    long QueryRequests);

public sealed record TelegramBridgeDependencyMetricsDto(
    string Dependency,
    long Calls,
    long Successes,
    long Failures,
    double AvgLatencyMs,
    double P95LatencyMs);

public sealed record TelegramBridgeErrorMetricsDto(
    string ErrorCode,
    long Count);

public sealed record TelegramBridgeRecentIncidentDto(
    DateTime OccurredAtUtc,
    string Stage,
    string ErrorCode,
    string? CorrelationId,
    string? Message);

public sealed class TelegramBridgeObservabilityResult
{
    public bool Success { get; init; }
    public int HttpStatusCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public TelegramBridgeObservabilitySnapshotDto? Snapshot { get; init; }

    public static TelegramBridgeObservabilityResult Ok(TelegramBridgeObservabilitySnapshotDto snapshot) =>
        new()
        {
            Success = true,
            HttpStatusCode = StatusCodes.Status200OK,
            Message = "Diagnostico do Telegram Bridge carregado.",
            Snapshot = snapshot
        };

    public static TelegramBridgeObservabilityResult Failed(int httpStatusCode, string message) =>
        new()
        {
            Success = false,
            HttpStatusCode = httpStatusCode,
            Message = message
        };
}
