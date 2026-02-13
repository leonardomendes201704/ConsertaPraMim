using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminRequestProposalService
{
    Task<AdminServiceRequestsListResponseDto> GetServiceRequestsAsync(AdminServiceRequestsQueryDto query);
    Task<AdminServiceRequestDetailsDto?> GetServiceRequestByIdAsync(Guid requestId);
    Task<AdminOperationResultDto> UpdateServiceRequestStatusAsync(
        Guid requestId,
        AdminUpdateServiceRequestStatusRequestDto request,
        Guid actorUserId,
        string actorEmail);

    Task<AdminProposalsListResponseDto> GetProposalsAsync(AdminProposalsQueryDto query);
    Task<AdminOperationResultDto> InvalidateProposalAsync(
        Guid proposalId,
        AdminInvalidateProposalRequestDto request,
        Guid actorUserId,
        string actorEmail);
}
