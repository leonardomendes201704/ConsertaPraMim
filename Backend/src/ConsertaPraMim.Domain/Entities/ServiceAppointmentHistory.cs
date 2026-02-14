using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ServiceAppointmentHistory : BaseEntity
{
    public Guid ServiceAppointmentId { get; set; }
    public ServiceAppointment ServiceAppointment { get; set; } = null!;

    public ServiceAppointmentStatus? PreviousStatus { get; set; }
    public ServiceAppointmentStatus NewStatus { get; set; }
    public ServiceAppointmentOperationalStatus? PreviousOperationalStatus { get; set; }
    public ServiceAppointmentOperationalStatus? NewOperationalStatus { get; set; }

    public Guid? ActorUserId { get; set; }
    public User? ActorUser { get; set; }
    public ServiceAppointmentActorRole ActorRole { get; set; }

    public string? Reason { get; set; }
    public string? Metadata { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
