using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminNoShowDashboardService
{
    Task<AdminNoShowDashboardDto> GetDashboardAsync(AdminNoShowDashboardQueryDto query);
    Task<AdminKpiCardDto> GetKpiAsync(AdminNoShowDashboardQueryDto query, string kpiKey);
    Task<string> ExportDashboardCsvAsync(AdminNoShowDashboardQueryDto query);
}
