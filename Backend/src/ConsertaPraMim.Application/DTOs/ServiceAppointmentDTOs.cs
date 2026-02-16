namespace ConsertaPraMim.Application.DTOs;

public record GetServiceAppointmentSlotsQueryDto(
    Guid ProviderId,
    DateTime FromUtc,
    DateTime ToUtc,
    int? SlotDurationMinutes = null);

public record CreateServiceAppointmentRequestDto(
    Guid ServiceRequestId,
    Guid ProviderId,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    string? Reason = null);

public record RejectServiceAppointmentRequestDto(string Reason);

public record RequestServiceAppointmentRescheduleDto(
    DateTime ProposedWindowStartUtc,
    DateTime ProposedWindowEndUtc,
    string Reason);

public record RespondServiceAppointmentRescheduleRequestDto(
    bool Accept,
    string? Reason = null);

public record CancelServiceAppointmentRequestDto(string Reason);

public record MarkServiceAppointmentArrivalRequestDto(
    double? Latitude,
    double? Longitude,
    double? AccuracyMeters,
    string? ManualReason = null);

public record StartServiceAppointmentExecutionRequestDto(
    string? Reason = null);

public record UpdateServiceAppointmentOperationalStatusRequestDto(
    string Status,
    string? Reason = null);

public record CreateServiceScopeChangeRequestDto(
    string Reason,
    string AdditionalScopeDescription,
    decimal IncrementalValue);

public record CreateServiceWarrantyClaimRequestDto(
    string IssueDescription);

public record CreateServiceDisputeCaseRequestDto(
    string Type,
    string ReasonCode,
    string Description,
    string? InitialMessage = null);

public record ScheduleServiceWarrantyRevisitRequestDto(
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    string? Reason = null);

public record RespondServiceWarrantyClaimRequestDto(
    bool Accept,
    string? Reason = null);

public record RegisterServiceScopeChangeAttachmentDto(
    string FileUrl,
    string FileName,
    string ContentType,
    long SizeBytes);

public record RejectServiceScopeChangeRequestDto(string Reason);

public record RespondServiceAppointmentPresenceRequestDto(
    bool Confirmed,
    string? Reason = null);

public record GenerateServiceCompletionPinRequestDto(
    bool ForceRegenerate = false,
    string? Reason = null);

public record ValidateServiceCompletionPinRequestDto(string Pin);

public record ConfirmServiceCompletionRequestDto(
    string Method,
    string? Pin = null,
    string? SignatureName = null);

public record ContestServiceCompletionRequestDto(string Reason);

public record ServiceAppointmentSlotDto(
    DateTime WindowStartUtc,
    DateTime WindowEndUtc);

public record ServiceAppointmentHistoryDto(
    Guid Id,
    string? PreviousStatus,
    string NewStatus,
    Guid? ActorUserId,
    string ActorRole,
    string? Reason,
    DateTime OccurredAtUtc,
    string? PreviousOperationalStatus = null,
    string? NewOperationalStatus = null,
    string? Metadata = null);

public record ServiceAppointmentDto(
    Guid Id,
    Guid ServiceRequestId,
    Guid ClientId,
    Guid ProviderId,
    string Status,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    DateTime? ExpiresAtUtc,
    string? Reason,
    DateTime? ProposedWindowStartUtc,
    DateTime? ProposedWindowEndUtc,
    DateTime? RescheduleRequestedAtUtc,
    string? RescheduleRequestedByRole,
    string? RescheduleRequestReason,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<ServiceAppointmentHistoryDto> History,
    DateTime? ArrivedAtUtc = null,
    double? ArrivedLatitude = null,
    double? ArrivedLongitude = null,
    double? ArrivedAccuracyMeters = null,
    string? ArrivedManualReason = null,
    DateTime? StartedAtUtc = null,
    string? OperationalStatus = null,
    DateTime? OperationalStatusUpdatedAtUtc = null,
    string? OperationalStatusReason = null,
    bool? ClientPresenceConfirmed = null,
    DateTime? ClientPresenceRespondedAtUtc = null,
    string? ClientPresenceReason = null,
    bool? ProviderPresenceConfirmed = null,
    DateTime? ProviderPresenceRespondedAtUtc = null,
    string? ProviderPresenceReason = null,
    int? NoShowRiskScore = null,
    string? NoShowRiskLevel = null,
    DateTime? NoShowRiskCalculatedAtUtc = null,
    string? NoShowRiskReasons = null);

public record ServiceAppointmentOperationResultDto(
    bool Success,
    ServiceAppointmentDto? Appointment = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record ServiceScopeChangeRequestDto(
    Guid Id,
    Guid ServiceRequestId,
    Guid ServiceAppointmentId,
    Guid ProviderId,
    int Version,
    string Status,
    string Reason,
    string AdditionalScopeDescription,
    decimal IncrementalValue,
    DateTime RequestedAtUtc,
    DateTime? ClientRespondedAtUtc,
    string? ClientResponseReason,
    Guid? PreviousVersionId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<ServiceScopeChangeAttachmentDto> Attachments);

public record ServiceScopeChangeAttachmentDto(
    Guid Id,
    Guid ServiceScopeChangeRequestId,
    Guid UploadedByUserId,
    string FileUrl,
    string FileName,
    string ContentType,
    string MediaKind,
    long SizeBytes,
    DateTime CreatedAt);

public record ServiceScopeChangeRequestOperationResultDto(
    bool Success,
    ServiceScopeChangeRequestDto? ScopeChangeRequest = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record ServiceScopeChangeAttachmentOperationResultDto(
    bool Success,
    ServiceScopeChangeAttachmentDto? Attachment = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record ServiceWarrantyClaimDto(
    Guid Id,
    Guid ServiceRequestId,
    Guid ServiceAppointmentId,
    Guid ClientId,
    Guid ProviderId,
    Guid? RevisitAppointmentId,
    string Status,
    string IssueDescription,
    string? ProviderResponseReason,
    string? AdminEscalationReason,
    DateTime RequestedAtUtc,
    DateTime WarrantyWindowEndsAtUtc,
    DateTime ProviderResponseDueAtUtc,
    DateTime? ProviderRespondedAtUtc,
    DateTime? EscalatedAtUtc,
    DateTime? ClosedAtUtc,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? MetadataJson = null);

public record ServiceWarrantyClaimOperationResultDto(
    bool Success,
    ServiceWarrantyClaimDto? WarrantyClaim = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record ServiceWarrantyRevisitOperationResultDto(
    bool Success,
    ServiceWarrantyClaimDto? WarrantyClaim = null,
    ServiceAppointmentDto? RevisitAppointment = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record ServiceDisputeCaseDto(
    Guid Id,
    Guid ServiceRequestId,
    Guid ServiceAppointmentId,
    Guid OpenedByUserId,
    string OpenedByRole,
    Guid CounterpartyUserId,
    string CounterpartyRole,
    Guid? OwnedByAdminUserId,
    DateTime? OwnedAtUtc,
    string Type,
    string Priority,
    string Status,
    string? WaitingForRole,
    string ReasonCode,
    string Description,
    DateTime OpenedAtUtc,
    DateTime SlaDueAtUtc,
    DateTime LastInteractionAtUtc,
    DateTime? ClosedAtUtc,
    string? ResolutionSummary,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<ServiceDisputeCaseMessageDto> Messages,
    IReadOnlyList<ServiceDisputeCaseAttachmentDto> Attachments,
    IReadOnlyList<ServiceDisputeCaseAuditEntryDto> AuditEntries,
    string? MetadataJson = null);

public record ServiceDisputeCaseMessageDto(
    Guid Id,
    Guid ServiceDisputeCaseId,
    Guid? AuthorUserId,
    string AuthorRole,
    string MessageType,
    string MessageText,
    bool IsInternal,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? MetadataJson = null);

public record ServiceDisputeCaseAttachmentDto(
    Guid Id,
    Guid ServiceDisputeCaseId,
    Guid? ServiceDisputeCaseMessageId,
    Guid UploadedByUserId,
    string FileUrl,
    string FileName,
    string ContentType,
    string MediaKind,
    long SizeBytes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record ServiceDisputeCaseAuditEntryDto(
    Guid Id,
    Guid ServiceDisputeCaseId,
    Guid? ActorUserId,
    string ActorRole,
    string EventType,
    string? Message,
    string? MetadataJson,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record ServiceDisputeCaseOperationResultDto(
    bool Success,
    ServiceDisputeCaseDto? DisputeCase = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record ServiceCompletionTermDto(
    Guid Id,
    Guid ServiceRequestId,
    Guid ServiceAppointmentId,
    Guid ProviderId,
    Guid ClientId,
    string Status,
    string? AcceptedWithMethod,
    DateTime? PinExpiresAtUtc,
    int PinFailedAttempts,
    DateTime? AcceptedAtUtc,
    DateTime? ContestedAtUtc,
    DateTime? EscalatedAtUtc,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? Summary = null,
    string? AcceptedSignatureName = null,
    string? ContestReason = null);

public record ServiceCompletionPinResultDto(
    bool Success,
    ServiceCompletionTermDto? Term = null,
    string? OneTimePin = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record ServiceAppointmentSlotsResultDto(
    bool Success,
    IReadOnlyList<ServiceAppointmentSlotDto> Slots,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record CreateProviderAvailabilityRuleRequestDto(
    Guid ProviderId,
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    TimeSpan EndTime,
    int SlotDurationMinutes = 30);

public record CreateProviderAvailabilityExceptionRequestDto(
    Guid ProviderId,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    string? Reason = null);

public record ProviderAvailabilityRuleDto(
    Guid Id,
    Guid ProviderId,
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    TimeSpan EndTime,
    int SlotDurationMinutes,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record ProviderAvailabilityExceptionDto(
    Guid Id,
    Guid ProviderId,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    string? Reason,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record ProviderAvailabilityOverviewDto(
    Guid ProviderId,
    IReadOnlyList<ProviderAvailabilityRuleDto> Rules,
    IReadOnlyList<ProviderAvailabilityExceptionDto> Blocks);

public record ProviderAvailabilityOverviewResultDto(
    bool Success,
    ProviderAvailabilityOverviewDto? Overview = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record ProviderAvailabilityOperationResultDto(
    bool Success,
    string? ErrorCode = null,
    string? ErrorMessage = null);
