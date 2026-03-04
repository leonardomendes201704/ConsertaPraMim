using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public record GoogleCalendarSyncOverviewDto(
    DateTime GeneratedAtUtc,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Total,
    int PendingCount,
    int SyncedCount,
    int FailedRetryableCount,
    int DeadLetterCount,
    int DeletedCount,
    int RetryQueueCount,
    int RetryInLast24hCount,
    double AverageLatencyMs,
    double P95LatencyMs,
    IReadOnlyList<GoogleCalendarSyncStatusBucketDto> StatusBuckets);

public record GoogleCalendarSyncStatusBucketDto(
    ServiceAppointmentCalendarSyncStatus Status,
    int Count);

public record GoogleCalendarSyncReprocessRequestDto(
    Guid? AppointmentId = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    IReadOnlyList<ServiceAppointmentCalendarSyncStatus>? Statuses = null,
    int MaxItems = 200,
    bool ForceResetRetry = false);

public record GoogleCalendarSyncReprocessResultDto(
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    int RequestedCount,
    int ProcessedCount,
    int SuccessCount,
    int FailedCount,
    int DeadLetterCount,
    IReadOnlyList<GoogleCalendarSyncReprocessItemResultDto> Items);

public record GoogleCalendarSyncReprocessItemResultDto(
    Guid AppointmentId,
    ServiceAppointmentCalendarSyncOperation Operation,
    ServiceAppointmentCalendarSyncStatus SyncStatus,
    int RetryCount,
    DateTime? NextRetryAtUtc,
    DateTime? LastSyncAtUtc,
    double? LastLatencyMs,
    string? LastErrorCode,
    string? Error,
    string? GoogleEventId);
