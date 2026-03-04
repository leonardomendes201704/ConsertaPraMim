using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ServiceAppointmentCalendarSync : BaseEntity
{
    public Guid AppointmentId { get; set; }
    public ServiceAppointment Appointment { get; set; } = null!;

    public string? GoogleEventId { get; set; }
    public ServiceAppointmentCalendarSyncStatus SyncStatus { get; set; } = ServiceAppointmentCalendarSyncStatus.Pending;
    public DateTime? LastSyncAtUtc { get; set; }
    public string? Error { get; set; }
}
