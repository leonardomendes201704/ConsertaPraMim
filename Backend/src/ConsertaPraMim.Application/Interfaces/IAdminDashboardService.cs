using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminDashboardService
{
    Task<AdminDashboardDto> GetDashboardAsync(AdminDashboardQueryDto query);
    Task<AdminCoverageMapDto> GetCoverageMapAsync(string? city = null);
}
