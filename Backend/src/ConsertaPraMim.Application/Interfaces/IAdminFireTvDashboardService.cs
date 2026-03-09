using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminFireTvDashboardService
{
    Task<AdminFireTvLandingDashboardDto> GetLandingDashboardAsync(
        int? rangeDays = null,
        CancellationToken cancellationToken = default);
}
