using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IFireTvDashboardHealthProbe
{
    Task<IReadOnlyList<AdminFireTvHealthTargetStatusDto>> ProbeAsync(
        IReadOnlyList<FireTvDashboardHealthTargetConfigDto> targets,
        int timeoutMs,
        CancellationToken cancellationToken = default);
}
