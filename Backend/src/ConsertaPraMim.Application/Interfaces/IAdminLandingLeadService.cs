using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminLandingLeadService
{
    Task<AdminLandingLeadsListResponseDto> GetLandingLeadsAsync(AdminLandingLeadsQueryDto query);
    Task<AdminLandingLeadDetailsDto?> GetLandingLeadByIdAsync(Guid leadId);
}
