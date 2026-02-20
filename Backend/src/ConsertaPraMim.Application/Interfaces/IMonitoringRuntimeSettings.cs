using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IMonitoringRuntimeSettings
{
    Task<AdminMonitoringRuntimeConfigDto> GetTelemetryConfigAsync(
        CancellationToken cancellationToken = default);

    Task<bool> IsTelemetryEnabledAsync(
        CancellationToken cancellationToken = default);

    void InvalidateTelemetryCache();
}
