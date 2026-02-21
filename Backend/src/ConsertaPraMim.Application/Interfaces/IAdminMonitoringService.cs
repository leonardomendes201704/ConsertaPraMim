using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminMonitoringService
{
    Task<int> SaveRawEventsAsync(
        IReadOnlyCollection<ApiRequestTelemetryEventDto> events,
        CancellationToken cancellationToken = default);

    Task<AdminMonitoringMaintenanceResultDto> RebuildAggregatesAndRetentionAsync(
        AdminMonitoringMaintenanceOptionsDto options,
        CancellationToken cancellationToken = default);

    Task<AdminMonitoringOverviewDto> GetOverviewAsync(
        AdminMonitoringOverviewQueryDto query,
        CancellationToken cancellationToken = default);

    Task<AdminMonitoringTopEndpointsResponseDto> GetTopEndpointsAsync(
        AdminMonitoringTopEndpointsQueryDto query,
        CancellationToken cancellationToken = default);

    Task<AdminMonitoringLatencyResponseDto> GetLatencyAsync(
        AdminMonitoringLatencyQueryDto query,
        CancellationToken cancellationToken = default);

    Task<AdminMonitoringErrorsResponseDto> GetErrorsAsync(
        AdminMonitoringErrorsQueryDto query,
        CancellationToken cancellationToken = default);

    Task<AdminMonitoringErrorDetailsDto?> GetErrorDetailsAsync(
        AdminMonitoringErrorDetailsQueryDto query,
        CancellationToken cancellationToken = default);

    Task<AdminMonitoringRequestsResponseDto> GetRequestsAsync(
        AdminMonitoringRequestsQueryDto query,
        CancellationToken cancellationToken = default);

    Task<AdminMonitoringRequestsExportResponseDto> ExportRequestsCsvBase64Async(
        AdminMonitoringRequestsQueryDto query,
        CancellationToken cancellationToken = default);

    Task<AdminMonitoringRequestDetailsDto?> GetRequestByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<AdminMonitoringRuntimeConfigDto> GetRuntimeConfigAsync(
        CancellationToken cancellationToken = default);

    Task<AdminMonitoringRuntimeConfigDto> SetTelemetryEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default);

    Task<AdminCorsRuntimeConfigDto> GetCorsConfigAsync(
        CancellationToken cancellationToken = default);

    Task<AdminCorsRuntimeConfigDto> SetCorsConfigAsync(
        IReadOnlyCollection<string> allowedOrigins,
        CancellationToken cancellationToken = default);

    Task<AdminRuntimeConfigSectionsResponseDto> GetConfigSectionsAsync(
        CancellationToken cancellationToken = default);

    Task<AdminRuntimeConfigSectionDto> SetConfigSectionAsync(
        string sectionPath,
        string jsonValue,
        string? securityCode = null,
        CancellationToken cancellationToken = default);
}
