using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ServiceAppointmentCalendarSync : BaseEntity
{
    public Guid AppointmentId { get; set; }
    public ServiceAppointment Appointment { get; set; } = null!;

    public string? GoogleEventId { get; set; }
    public ServiceAppointmentCalendarSyncOperation LastOperation { get; set; } = ServiceAppointmentCalendarSyncOperation.Unknown;
    public ServiceAppointmentCalendarSyncStatus SyncStatus { get; set; } = ServiceAppointmentCalendarSyncStatus.Pending;
    public int RetryCount { get; set; }
    public int MaxRetryAttempts { get; set; } = 5;
    public DateTime? NextRetryAtUtc { get; set; }
    public DateTime? DeadLetterAtUtc { get; set; }
    public DateTime? LastSyncAtUtc { get; set; }
    public double? LastLatencyMs { get; set; }
    public string? LastErrorCode { get; set; }
    public string? Error { get; set; }
}
