using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminDashboardService
{
    Task<AdminDashboardDto> GetDashboardAsync(AdminDashboardQueryDto query);
    Task<AdminKpiCardDto> GetKpiAsync(AdminDashboardQueryDto query, string kpiKey);
    Task<AdminCoverageMapDto> GetCoverageMapAsync(string? city = null);
}
