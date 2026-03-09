using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface ILandingAnalyticsRuntimeSettings
{
    Task<LandingAnalyticsRuntimeConfigDto> GetConfigAsync(CancellationToken cancellationToken = default);
    Task<LandingAnalyticsPublicConfigDto> GetPublicConfigAsync(CancellationToken cancellationToken = default);
    void InvalidateCache();
}
