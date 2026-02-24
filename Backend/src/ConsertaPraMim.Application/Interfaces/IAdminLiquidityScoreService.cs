using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminLiquidityScoreService
{
    Task<AdminLiquidityScoreResponseDto> GetScoreAsync(AdminLiquidityScoreQueryDto query);
}
