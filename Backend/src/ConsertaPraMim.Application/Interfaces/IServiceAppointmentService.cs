using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IServiceAppointmentService
{
    Task<ServiceAppointmentSlotsResultDto> GetAvailableSlotsAsync(
        Guid actorUserId,
        string actorRole,
        GetServiceAppointmentSlotsQueryDto query);

    Task<ProviderAvailabilityOverviewResultDto> GetProviderAvailabilityOverviewAsync(
        Guid actorUserId,
        string actorRole,
        Guid providerId);

    Task<ProviderAvailabilityOperationResultDto> AddProviderAvailabilityRuleAsync(
        Guid actorUserId,
        string actorRole,
        CreateProviderAvailabilityRuleRequestDto request);

    Task<ProviderAvailabilityOperationResultDto> RemoveProviderAvailabilityRuleAsync(
        Guid actorUserId,
        string actorRole,
        Guid ruleId);

    Task<ProviderAvailabilityOperationResultDto> AddProviderAvailabilityExceptionAsync(
        Guid actorUserId,
        string actorRole,
        CreateProviderAvailabilityExceptionRequestDto request);

    Task<ProviderAvailabilityOperationResultDto> RemoveProviderAvailabilityExceptionAsync(
        Guid actorUserId,
        string actorRole,
        Guid exceptionId);

    Task<ServiceAppointmentOperationResultDto> CreateAsync(
        Guid actorUserId,
        string actorRole,
        CreateServiceAppointmentRequestDto request);

    Task<ServiceAppointmentOperationResultDto> ConfirmAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId);

    Task<ServiceAppointmentOperationResultDto> RejectAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        RejectServiceAppointmentRequestDto request);

    Task<ServiceAppointmentOperationResultDto> RequestRescheduleAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        RequestServiceAppointmentRescheduleDto request);

    Task<ServiceAppointmentOperationResultDto> RespondRescheduleAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        RespondServiceAppointmentRescheduleRequestDto request);

    Task<ServiceAppointmentOperationResultDto> CancelAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        CancelServiceAppointmentRequestDto request);

    Task<ServiceAppointmentOperationResultDto> OverrideFinancialPolicyAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        ServiceFinancialPolicyOverrideRequestDto request);

    Task<ServiceAppointmentOperationResultDto> MarkArrivedAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        MarkServiceAppointmentArrivalRequestDto request);

    Task<ServiceAppointmentOperationResultDto> StartExecutionAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        StartServiceAppointmentExecutionRequestDto request);

    Task<ServiceAppointmentOperationResultDto> RespondPresenceAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        RespondServiceAppointmentPresenceRequestDto request);

    Task<ServiceAppointmentOperationResultDto> UpdateOperationalStatusAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        UpdateServiceAppointmentOperationalStatusRequestDto request);

    Task<ServiceScopeChangeRequestOperationResultDto> CreateScopeChangeRequestAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        CreateServiceScopeChangeRequestDto request);

    Task<ServiceWarrantyClaimOperationResultDto> CreateWarrantyClaimAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        CreateServiceWarrantyClaimRequestDto request);

    Task<ServiceDisputeCaseOperationResultDto> CreateDisputeCaseAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        CreateServiceDisputeCaseRequestDto request);

    Task<ServiceWarrantyRevisitOperationResultDto> ScheduleWarrantyRevisitAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        Guid warrantyClaimId,
        ScheduleServiceWarrantyRevisitRequestDto request);

    Task<ServiceWarrantyClaimOperationResultDto> RespondWarrantyClaimAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        Guid warrantyClaimId,
        RespondServiceWarrantyClaimRequestDto request);

    Task<ServiceScopeChangeAttachmentOperationResultDto> AddScopeChangeAttachmentAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        Guid scopeChangeRequestId,
        RegisterServiceScopeChangeAttachmentDto request);

    Task<ServiceScopeChangeRequestOperationResultDto> ApproveScopeChangeRequestAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        Guid scopeChangeRequestId);

    Task<ServiceScopeChangeRequestOperationResultDto> RejectScopeChangeRequestAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        Guid scopeChangeRequestId,
        RejectServiceScopeChangeRequestDto request);

    Task<IReadOnlyList<ServiceScopeChangeRequestDto>> GetScopeChangeRequestsByServiceRequestAsync(
        Guid actorUserId,
        string actorRole,
        Guid serviceRequestId);

    Task<IReadOnlyList<ServiceWarrantyClaimDto>> GetWarrantyClaimsByServiceRequestAsync(
        Guid actorUserId,
        string actorRole,
        Guid serviceRequestId);

    Task<ServiceCompletionPinResultDto> GenerateCompletionPinAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        GenerateServiceCompletionPinRequestDto request);

    Task<ServiceCompletionPinResultDto> ValidateCompletionPinAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        ValidateServiceCompletionPinRequestDto request);

    Task<ServiceCompletionPinResultDto> ConfirmCompletionAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        ConfirmServiceCompletionRequestDto request);

    Task<ServiceCompletionPinResultDto> ContestCompletionAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        ContestServiceCompletionRequestDto request);

    Task<ServiceCompletionPinResultDto> GetCompletionTermAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId);

    Task<int> ExpirePendingAppointmentsAsync(int batchSize = 200);

    Task<int> ExpirePendingScopeChangeRequestsAsync(int batchSize = 200);

    Task<int> EscalateWarrantyClaimsBySlaAsync(int batchSize = 200);

    Task<ServiceAppointmentOperationResultDto> GetByIdAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId);

    Task<IReadOnlyList<ServiceAppointmentDto>> GetMyAppointmentsAsync(
        Guid actorUserId,
        string actorRole,
        DateTime? fromUtc = null,
        DateTime? toUtc = null);
}
