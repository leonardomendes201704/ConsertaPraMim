using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminNoShowDashboardService
{
    Task<AdminNoShowDashboardDto> GetDashboardAsync(AdminNoShowDashboardQueryDto query);
}
