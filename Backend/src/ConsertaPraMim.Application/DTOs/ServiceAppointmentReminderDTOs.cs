using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public record AppointmentReminderDispatchDto(
    Guid Id,
    Guid ServiceAppointmentId,
    Guid RecipientUserId,
    string RecipientEmail,
    AppointmentReminderChannel Channel,
    AppointmentReminderDispatchStatus Status,
    int ReminderOffsetMinutes,
    DateTime ScheduledForUtc,
    DateTime NextAttemptAtUtc,
    int AttemptCount,
    int MaxAttempts,
    string EventKey,
    string Subject,
    string Message,
    string? ActionUrl,
    DateTime? LastAttemptAtUtc,
    DateTime? SentAtUtc,
    DateTime? DeliveredAtUtc,
    DateTime? ResponseReceivedAtUtc,
    bool? ResponseConfirmed,
    string? ResponseReason,
    DateTime? CancelledAtUtc,
    string? LastError,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record AppointmentReminderDispatchQueryDto(
    Guid? AppointmentId = null,
    AppointmentReminderDispatchStatus? Status = null,
    AppointmentReminderChannel? Channel = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int Page = 1,
    int PageSize = 50);

public record AppointmentReminderDispatchListResultDto(
    IReadOnlyList<AppointmentReminderDispatchDto> Items,
    int Total,
    int Page,
    int PageSize);
