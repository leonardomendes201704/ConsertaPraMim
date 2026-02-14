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
    DateTime OccurredAtUtc);

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
    IReadOnlyList<ServiceAppointmentHistoryDto> History);

public record ServiceAppointmentOperationResultDto(
    bool Success,
    ServiceAppointmentDto? Appointment = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record ServiceAppointmentSlotsResultDto(
    bool Success,
    IReadOnlyList<ServiceAppointmentSlotDto> Slots,
    string? ErrorCode = null,
    string? ErrorMessage = null);
