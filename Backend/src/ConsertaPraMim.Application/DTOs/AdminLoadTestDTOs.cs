using System.Text.Json;

namespace ConsertaPraMim.Application.DTOs;

public record AdminLoadTestRunsQueryDto(
    string? Scenario = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);

public record AdminLoadTestRunListItemDto(
    Guid Id,
    string ExternalRunId,
    string Scenario,
    string BaseUrl,
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    double DurationSeconds,
    long TotalRequests,
    long SuccessfulRequests,
    long FailedRequests,
    double ErrorRatePercent,
    double RpsAvg,
    int RpsPeak,
    double P95LatencyMs,
    string Source,
    DateTime CreatedAt);

public record AdminLoadTestRunsResponseDto(
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<AdminLoadTestRunListItemDto> Items);

public record AdminLoadTestStatusCodeSnapshotDto(
    int StatusCode,
    long Count,
    double Percentage);

public record AdminLoadTestEndpointSnapshotDto(
    string Endpoint,
    long Hits,
    long Errors,
    double ErrorRatePercent,
    double AvgLatencyMs,
    double P95LatencyMs);

public record AdminLoadTestErrorSnapshotDto(
    string Message,
    long Count,
    IReadOnlyList<string> Endpoints);

public record AdminLoadTestFailureSampleSnapshotDto(
    DateTime? TimestampUtc,
    string Method,
    string Path,
    int? StatusCode,
    string CorrelationId,
    string ErrorType,
    string ErrorMessage);

public record AdminLoadTestRunDetailsDto(
    Guid Id,
    string ExternalRunId,
    string Scenario,
    string BaseUrl,
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    double DurationSeconds,
    long TotalRequests,
    long SuccessfulRequests,
    long FailedRequests,
    double ErrorRatePercent,
    double RpsAvg,
    int RpsPeak,
    double MinLatencyMs,
    double P50LatencyMs,
    double P95LatencyMs,
    double P99LatencyMs,
    double MaxLatencyMs,
    string Source,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<AdminLoadTestStatusCodeSnapshotDto> StatusCodes,
    IReadOnlyList<AdminLoadTestEndpointSnapshotDto> TopEndpointsByHits,
    IReadOnlyList<AdminLoadTestEndpointSnapshotDto> TopEndpointsByP95,
    IReadOnlyList<AdminLoadTestErrorSnapshotDto> TopErrors,
    IReadOnlyList<AdminLoadTestFailureSampleSnapshotDto> FailureSamples,
    string RawReportJson);

public record AdminLoadTestImportRequestDto(
    string? Source,
    JsonElement Report);

public record AdminLoadTestImportResultDto(
    Guid Id,
    string ExternalRunId,
    bool Created,
    string Message);
