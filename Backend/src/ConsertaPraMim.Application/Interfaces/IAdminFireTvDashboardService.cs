using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminFireTvDashboardService
{
    Task<AdminFireTvLandingDashboardDto> GetLandingDashboardAsync(
        int? rangeDays = null,
        string? origin = null,
        string? comparisonMode = null,
        CancellationToken cancellationToken = default);

    Task<AdminFireTvOperationsDashboardDto> GetOperationsDashboardAsync(
        CancellationToken cancellationToken = default);
}
