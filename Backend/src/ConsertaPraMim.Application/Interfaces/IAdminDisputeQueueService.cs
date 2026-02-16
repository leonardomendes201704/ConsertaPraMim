using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminDisputeQueueService
{
    Task<AdminDisputesQueueResponseDto> GetQueueAsync(
        Guid? highlightedDisputeCaseId,
        int take = 100,
        string? status = null,
        string? type = null,
        Guid? operatorAdminId = null,
        string? operatorScope = null,
        string? sla = null);
    Task<string> ExportQueueCsvAsync(
        Guid? highlightedDisputeCaseId,
        int take = 200,
        string? status = null,
        string? type = null,
        Guid? operatorAdminId = null,
        string? operatorScope = null,
        string? sla = null);
    Task<AdminDisputeObservabilityDashboardDto> GetObservabilityAsync(AdminDisputeObservabilityQueryDto query);
    Task<AdminDisputeCaseDetailsDto?> GetCaseDetailsAsync(Guid disputeCaseId);
    Task<AdminDisputeOperationResultDto> UpdateWorkflowAsync(
        Guid disputeCaseId,
        Guid actorUserId,
        string actorEmail,
        AdminUpdateDisputeWorkflowRequestDto request);
    Task<AdminDisputeOperationResultDto> RegisterDecisionAsync(
        Guid disputeCaseId,
        Guid actorUserId,
        string actorEmail,
        AdminRegisterDisputeDecisionRequestDto request);
}
