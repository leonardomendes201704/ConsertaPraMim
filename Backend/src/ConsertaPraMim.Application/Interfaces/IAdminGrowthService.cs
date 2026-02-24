using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminGrowthService
{
    Task<AdminGrowthFunnelDto> GetFunnelAsync(AdminGrowthFunnelQueryDto query);
}
