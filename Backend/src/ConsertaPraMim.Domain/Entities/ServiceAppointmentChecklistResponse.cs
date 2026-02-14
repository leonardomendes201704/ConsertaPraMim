using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class ServiceAppointmentChecklistResponse : BaseEntity
{
    public Guid ServiceAppointmentId { get; set; }
    public ServiceAppointment ServiceAppointment { get; set; } = null!;

    public Guid TemplateItemId { get; set; }
    public ServiceChecklistTemplateItem TemplateItem { get; set; } = null!;

    public bool IsChecked { get; set; }
    public string? Note { get; set; }

    public string? EvidenceUrl { get; set; }
    public string? EvidenceFileName { get; set; }
    public string? EvidenceContentType { get; set; }
    public long? EvidenceSizeBytes { get; set; }

    public Guid? CheckedByUserId { get; set; }
    public User? CheckedByUser { get; set; }
    public DateTime? CheckedAtUtc { get; set; }
}
