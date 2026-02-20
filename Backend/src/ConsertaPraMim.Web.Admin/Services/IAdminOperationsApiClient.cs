using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;

namespace ConsertaPraMim.Web.Admin.Services;

public interface IAdminOperationsApiClient
{
    Task<AdminApiResult<AdminServiceRequestsListResponseDto>> GetServiceRequestsAsync(
        AdminServiceRequestsFilterModel filters,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminServiceRequestDetailsDto>> GetServiceRequestByIdAsync(
        Guid requestId,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminOperationResultDto>> UpdateServiceRequestStatusAsync(
        Guid requestId,
        string status,
        string? reason,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminProposalsListResponseDto>> GetProposalsAsync(
        AdminProposalsFilterModel filters,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminOperationResultDto>> InvalidateProposalAsync(
        Guid proposalId,
        string? reason,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminChatsListResponseDto>> GetChatsAsync(
        AdminChatsFilterModel filters,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminChatDetailsDto>> GetChatAsync(
        Guid requestId,
        Guid providerId,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminChatAttachmentsListResponseDto>> GetChatAttachmentsAsync(
        AdminChatAttachmentsFilterModel filters,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminDisputesQueueResponseDto>> GetDisputesQueueAsync(
        AdminDisputesQueueFilterModel filters,
        string accessToken,
        CancellationToken cancellationToken = default);
    Task<AdminApiResult<AdminDisputeObservabilityDashboardDto>> GetDisputesObservabilityAsync(
        string accessToken,
        CancellationToken cancellationToken = default);
    Task<AdminApiResult<string>> ExportDisputesQueueCsvAsync(
        AdminDisputesQueueFilterModel filters,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminDisputeCaseDetailsDto>> GetDisputeByIdAsync(
        Guid disputeCaseId,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminDisputeOperationResultDto>> UpdateDisputeWorkflowAsync(
        Guid disputeCaseId,
        AdminUpdateDisputeWorkflowRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminDisputeOperationResultDto>> RegisterDisputeDecisionAsync(
        Guid disputeCaseId,
        AdminRegisterDisputeDecisionRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminSendNotificationResultDto>> SendNotificationAsync(
        Guid recipientUserId,
        string subject,
        string message,
        string? actionUrl,
        string? reason,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<Guid>> FindUserIdByEmailAsync(
        string email,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<IReadOnlyList<AdminServiceCategoryDto>>> GetServiceCategoriesAsync(
        bool includeInactive,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminServiceCategoryUpsertResultDto>> CreateServiceCategoryAsync(
        AdminCreateServiceCategoryRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminServiceCategoryUpsertResultDto>> UpdateServiceCategoryAsync(
        Guid categoryId,
        AdminUpdateServiceCategoryRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminOperationResultDto>> UpdateServiceCategoryStatusAsync(
        Guid categoryId,
        AdminUpdateServiceCategoryStatusRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<IReadOnlyList<AdminChecklistTemplateDto>>> GetChecklistTemplatesAsync(
        bool includeInactive,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminChecklistTemplateUpsertResultDto>> CreateChecklistTemplateAsync(
        AdminCreateChecklistTemplateRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminChecklistTemplateUpsertResultDto>> UpdateChecklistTemplateAsync(
        Guid templateId,
        AdminUpdateChecklistTemplateRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminOperationResultDto>> UpdateChecklistTemplateStatusAsync(
        Guid templateId,
        AdminUpdateChecklistTemplateStatusRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminPlanGovernanceSnapshotDto>> GetPlanGovernanceSnapshotAsync(
        bool includeInactivePromotions,
        bool includeInactiveCoupons,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminOperationResultDto>> UpdatePlanSettingAsync(
        string plan,
        AdminUpdatePlanSettingRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminOperationResultDto>> CreatePlanPromotionAsync(
        AdminCreatePlanPromotionRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminOperationResultDto>> UpdatePlanPromotionAsync(
        Guid promotionId,
        AdminUpdatePlanPromotionRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminOperationResultDto>> UpdatePlanPromotionStatusAsync(
        Guid promotionId,
        AdminUpdatePlanPromotionStatusRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminOperationResultDto>> CreatePlanCouponAsync(
        AdminCreatePlanCouponRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminOperationResultDto>> UpdatePlanCouponAsync(
        Guid couponId,
        AdminUpdatePlanCouponRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminOperationResultDto>> UpdatePlanCouponStatusAsync(
        Guid couponId,
        AdminUpdatePlanCouponStatusRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminPlanPriceSimulationResultDto>> SimulatePlanPriceAsync(
        AdminPlanPriceSimulationRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<ProviderCreditBalanceDto>> GetProviderCreditBalanceAsync(
        Guid providerId,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<ProviderCreditStatementDto>> GetProviderCreditStatementAsync(
        Guid providerId,
        AdminProviderCreditsFilterModel filters,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminProviderCreditUsageReportDto>> GetProviderCreditUsageReportAsync(
        AdminProviderCreditUsageReportQueryDto query,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminProviderCreditMutationResultDto>> GrantProviderCreditAsync(
        AdminProviderCreditGrantRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminProviderCreditMutationResultDto>> ReverseProviderCreditAsync(
        AdminProviderCreditReversalRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminMonitoringOverviewDto>> GetMonitoringOverviewAsync(
        AdminMonitoringOverviewQueryDto query,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminMonitoringTopEndpointsResponseDto>> GetMonitoringTopEndpointsAsync(
        AdminMonitoringTopEndpointsQueryDto query,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminMonitoringLatencyResponseDto>> GetMonitoringLatencyAsync(
        AdminMonitoringLatencyQueryDto query,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminMonitoringErrorsResponseDto>> GetMonitoringErrorsAsync(
        AdminMonitoringErrorsQueryDto query,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminMonitoringRequestsResponseDto>> GetMonitoringRequestsAsync(
        AdminMonitoringRequestsQueryDto query,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminMonitoringRequestsExportResponseDto>> ExportMonitoringRequestsCsvAsync(
        AdminMonitoringRequestsQueryDto query,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminMonitoringRequestDetailsDto>> GetMonitoringRequestDetailsAsync(
        string correlationId,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminMonitoringRuntimeConfigDto>> GetMonitoringRuntimeConfigAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminMonitoringRuntimeConfigDto>> SetMonitoringTelemetryEnabledAsync(
        bool enabled,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminLoadTestRunsResponseDto>> GetLoadTestRunsAsync(
        AdminLoadTestRunsQueryDto query,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminLoadTestRunDetailsDto>> GetLoadTestRunByIdAsync(
        Guid runId,
        string accessToken,
        CancellationToken cancellationToken = default);
}
