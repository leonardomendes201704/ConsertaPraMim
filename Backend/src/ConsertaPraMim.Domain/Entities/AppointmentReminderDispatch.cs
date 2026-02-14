using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class AppointmentReminderDispatch : BaseEntity
{
    public Guid ServiceAppointmentId { get; set; }
    public ServiceAppointment ServiceAppointment { get; set; } = null!;

    public Guid RecipientUserId { get; set; }
    public User RecipientUser { get; set; } = null!;

    public AppointmentReminderChannel Channel { get; set; } = AppointmentReminderChannel.InApp;
    public AppointmentReminderDispatchStatus Status { get; set; } = AppointmentReminderDispatchStatus.Pending;

    public int ReminderOffsetMinutes { get; set; }
    public DateTime ScheduledForUtc { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 3;

    public string EventKey { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }

    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? LastError { get; set; }
}
