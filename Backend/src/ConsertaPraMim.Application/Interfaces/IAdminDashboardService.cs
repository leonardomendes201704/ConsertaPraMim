using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminDashboardService
{
    Task<AdminDashboardDto> GetDashboardAsync(AdminDashboardQueryDto query);
    Task<AdminKpiCardDto> GetKpiAsync(AdminDashboardQueryDto query, string kpiKey);
    Task<AdminDashboardWidgetDto> GetWidgetAsync(AdminDashboardQueryDto query, string widgetKey);
    Task<AdminCoverageMapDto> GetCoverageMapAsync(string? city = null);
}
