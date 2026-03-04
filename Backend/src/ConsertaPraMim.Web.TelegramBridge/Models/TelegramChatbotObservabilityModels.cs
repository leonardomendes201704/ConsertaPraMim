namespace ConsertaPraMim.Web.TelegramBridge.Models;

public sealed record TelegramChatbotObservabilitySnapshotDto(
    DateTime GeneratedAtUtc,
    string Environment,
    TelegramChatbotTrafficMetricsDto Traffic,
    TelegramChatbotAiMetricsDto Ai,
    TelegramChatbotBusinessMetricsDto Business,
    IReadOnlyList<TelegramChatbotDependencyMetricsDto> Dependencies,
    IReadOnlyList<TelegramChatbotErrorMetricsDto> TopErrors,
    IReadOnlyList<TelegramChatbotRecentIncidentDto> RecentIncidents);

public sealed record TelegramChatbotTrafficMetricsDto(
    long InboundMessages,
    long OutboundMessages,
    long MessagesWithAttachments);

public sealed record TelegramChatbotAiMetricsDto(
    long Requests,
    long Fallbacks,
    long Failures,
    long GuardrailInterventions,
    long HumanHandoffs,
    double AvgLatencyMs,
    double P95LatencyMs,
    long Tokens,
    decimal EstimatedCostUsd);

public sealed record TelegramChatbotBusinessMetricsDto(
    long TriageRequestsOpened,
    long SchedulingAttempts,
    long SchedulingConfirmed,
    long SchedulingFailures,
    long QueryRequests);

public sealed record TelegramChatbotDependencyMetricsDto(
    string Dependency,
    long Calls,
    long Successes,
    long Failures,
    double AvgLatencyMs,
    double P95LatencyMs);

public sealed record TelegramChatbotErrorMetricsDto(
    string ErrorCode,
    long Count);

public sealed record TelegramChatbotRecentIncidentDto(
    DateTime OccurredAtUtc,
    string Stage,
    string ErrorCode,
    string? CorrelationId,
    string? Message);
