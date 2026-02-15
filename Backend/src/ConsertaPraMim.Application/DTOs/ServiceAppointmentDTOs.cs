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
    string? OperationalStatusReason = null);

public record ServiceAppointmentOperationResultDto(
    bool Success,
    ServiceAppointmentDto? Appointment = null,
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
