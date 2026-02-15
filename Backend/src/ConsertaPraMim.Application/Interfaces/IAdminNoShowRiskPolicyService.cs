using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminNoShowRiskPolicyService
{
    Task<AdminNoShowRiskPolicyDto?> GetActiveAsync();
    Task<AdminNoShowRiskPolicyUpdateResultDto> UpdateActiveAsync(
        AdminUpdateNoShowRiskPolicyRequestDto request,
        Guid actorUserId,
        string actorEmail);
}
