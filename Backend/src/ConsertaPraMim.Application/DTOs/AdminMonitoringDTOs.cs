namespace ConsertaPraMim.Application.DTOs;

public record ApiRequestTelemetryEventDto(
    DateTime TimestampUtc,
    string CorrelationId,
    string TraceId,
    string Method,
    string EndpointTemplate,
    string Path,
    int StatusCode,
    int DurationMs,
    string Severity,
    bool IsError,
    int WarningCount,
    string? WarningCodesJson,
    string? ErrorType,
    string? NormalizedErrorMessage,
    string? NormalizedErrorKey,
    string? IpHash,
    string? UserAgent,
    Guid? UserId,
    string? TenantId,
    long? RequestSizeBytes,
    long? ResponseSizeBytes,
    string Scheme,
    string? Host,
    string? RequestBodyJson = null,
    string? ResponseBodyJson = null,
    string? RequestHeadersJson = null,
    string? QueryStringJson = null,
    string? RouteValuesJson = null);

public record AdminMonitoringOverviewQueryDto(
    string? Range,
    string? Endpoint,
    int? StatusCode,
    Guid? UserId,
    string? TenantId,
    string? Severity);

public record AdminMonitoringTopEndpointsQueryDto(
    string? Range,
    int Take = 20,
    string? Endpoint = null,
    int? StatusCode = null,
    Guid? UserId = null,
    string? TenantId = null,
    string? Severity = null);

public record AdminMonitoringLatencyQueryDto(
    string? Endpoint,
    string? Range,
    int? StatusCode = null,
    Guid? UserId = null,
    string? TenantId = null,
    string? Severity = null);

public record AdminMonitoringErrorsQueryDto(
    string? Range,
    string? GroupBy = null,
    string? Endpoint = null,
    int? StatusCode = null,
    Guid? UserId = null,
    string? TenantId = null,
    string? Severity = null);

public record AdminMonitoringRequestsQueryDto(
    string? Range,
    string? Endpoint = null,
    int? StatusCode = null,
    Guid? UserId = null,
    string? TenantId = null,
    string? Severity = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 50);

public record AdminMonitoringOverviewDto(
    long TotalRequests,
    double ErrorRatePercent,
    int P95LatencyMs,
    double RequestsPerMinute,
    string TopEndpoint,
    IReadOnlyList<AdminMonitoringTimeseriesPointDto> RequestsSeries,
    IReadOnlyList<AdminMonitoringTimeseriesPointDto> ErrorsSeries,
    IReadOnlyList<AdminMonitoringLatencyTimeseriesPointDto> LatencySeries,
    IReadOnlyList<AdminMonitoringStatusDistributionDto> StatusDistribution,
    IReadOnlyList<AdminMonitoringTopErrorDto> TopErrors,
    long ApiUptimeSeconds = 0,
    string ApiHealthStatus = "healthy",
    string DatabaseHealthStatus = "unknown",
    string ClientPortalHealthStatus = "unknown",
    string ProviderPortalHealthStatus = "unknown");

public record AdminMonitoringTimeseriesPointDto(
    DateTime BucketUtc,
    long Value);

public record AdminMonitoringLatencyTimeseriesPointDto(
    DateTime BucketUtc,
    int P50Ms,
    int P95Ms,
    int P99Ms);

public record AdminMonitoringStatusDistributionDto(
    int StatusCode,
    long Count);

public record AdminMonitoringTopEndpointDto(
    string Method,
    string EndpointTemplate,
    long Hits,
    double ErrorRatePercent,
    int P95LatencyMs,
    int P99LatencyMs,
    long WarningCount);

public record AdminMonitoringTopEndpointsResponseDto(
    IReadOnlyList<AdminMonitoringTopEndpointDto> Items);

public record AdminMonitoringLatencyResponseDto(
    string EndpointTemplate,
    IReadOnlyList<AdminMonitoringLatencyTimeseriesPointDto> Series,
    int P50Ms,
    int P95Ms,
    int P99Ms,
    int MinMs,
    int MaxMs);

public record AdminMonitoringTopErrorDto(
    string ErrorKey,
    string ErrorType,
    string Message,
    long Count,
    string? EndpointTemplate,
    int? StatusCode);

public record AdminMonitoringErrorsResponseDto(
    string GroupBy,
    IReadOnlyList<AdminMonitoringTopErrorDto> Items,
    IReadOnlyList<AdminMonitoringTimeseriesPointDto> Series);

public record AdminMonitoringRequestItemDto(
    Guid Id,
    DateTime TimestampUtc,
    string CorrelationId,
    string Method,
    string EndpointTemplate,
    int StatusCode,
    int DurationMs,
    string Severity,
    int WarningCount,
    string? ErrorType,
    string? NormalizedErrorMessage,
    Guid? UserId,
    string? TenantId,
    string? Scheme = null,
    string? Host = null,
    string? EnvironmentName = null);

public record AdminMonitoringRequestsResponseDto(
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<AdminMonitoringRequestItemDto> Items);

public record AdminMonitoringRequestsExportResponseDto(
    string FileName,
    string ContentType,
    string Base64Content,
    int TotalRows);

public record AdminMonitoringRuntimeConfigDto(
    bool TelemetryEnabled,
    DateTime UpdatedAtUtc);

public record AdminCorsRuntimeConfigDto(
    IReadOnlyList<string> AllowedOrigins,
    DateTime UpdatedAtUtc);

public record AdminMonitoringUpdateTelemetryRequestDto(
    bool Enabled);

public record AdminUpdateCorsConfigRequestDto(
    IReadOnlyList<string>? AllowedOrigins);

public record AdminMonitoringRequestDetailsDto(
    Guid Id,
    DateTime TimestampUtc,
    string CorrelationId,
    string TraceId,
    string Method,
    string EndpointTemplate,
    string Path,
    int StatusCode,
    int DurationMs,
    string Severity,
    bool IsError,
    int WarningCount,
    string? WarningCodesJson,
    string? ErrorType,
    string? NormalizedErrorMessage,
    string? NormalizedErrorKey,
    string? IpHash,
    string? UserAgent,
    Guid? UserId,
    string? TenantId,
    long? RequestSizeBytes,
    long? ResponseSizeBytes,
    string Scheme,
    string? Host,
    string? RequestBodyJson = null,
    string? ResponseBodyJson = null,
    string? EnvironmentName = null,
    string? RequestHeadersJson = null,
    string? QueryStringJson = null,
    string? RouteValuesJson = null);

public record AdminMonitoringMaintenanceOptionsDto(
    int HourlyRecomputeWindowHours,
    int DailyRecomputeWindowDays,
    int RawRetentionDays,
    int AggregateRetentionDays);

public record AdminMonitoringMaintenanceResultDto(
    int ProcessedRawLogs,
    int RecomputedHourlyBuckets,
    int RecomputedDailyBuckets,
    int UpdatedErrorCatalogEntries,
    int UpsertedErrorOccurrences,
    int PurgedRawLogs,
    int PurgedAggregateRows,
    int PurgedErrorOccurrences);
