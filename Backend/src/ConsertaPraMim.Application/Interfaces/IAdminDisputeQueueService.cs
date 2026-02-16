using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminDisputeQueueService
{
    Task<AdminDisputesQueueResponseDto> GetQueueAsync(Guid? highlightedDisputeCaseId, int take = 100);
    Task<AdminDisputeCaseDetailsDto?> GetCaseDetailsAsync(Guid disputeCaseId);
    Task<AdminDisputeOperationResultDto> UpdateWorkflowAsync(
        Guid disputeCaseId,
        Guid actorUserId,
        string actorEmail,
        AdminUpdateDisputeWorkflowRequestDto request);
}
