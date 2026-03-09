using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IFireTvDashboardRuntimeSettings
{
    Task<FireTvDashboardRuntimeConfigDto> GetConfigAsync(CancellationToken cancellationToken = default);
    void InvalidateCache();
}
