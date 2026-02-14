using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ServiceAppointmentChecklistHistory : BaseEntity
{
    public Guid ServiceAppointmentId { get; set; }
    public ServiceAppointment ServiceAppointment { get; set; } = null!;

    public Guid TemplateItemId { get; set; }
    public ServiceChecklistTemplateItem TemplateItem { get; set; } = null!;

    public bool? PreviousIsChecked { get; set; }
    public bool NewIsChecked { get; set; }

    public string? PreviousNote { get; set; }
    public string? NewNote { get; set; }

    public string? PreviousEvidenceUrl { get; set; }
    public string? NewEvidenceUrl { get; set; }

    public Guid ActorUserId { get; set; }
    public User ActorUser { get; set; } = null!;
    public ServiceAppointmentActorRole ActorRole { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
