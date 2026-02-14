using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class ServiceChecklistTemplateItem : BaseEntity
{
    public Guid TemplateId { get; set; }
    public ServiceChecklistTemplate Template { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? HelpText { get; set; }
    public bool IsRequired { get; set; } = true;
    public bool RequiresEvidence { get; set; }
    public bool AllowNote { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<ServiceAppointmentChecklistResponse> Responses { get; set; } = new List<ServiceAppointmentChecklistResponse>();
    public ICollection<ServiceAppointmentChecklistHistory> History { get; set; } = new List<ServiceAppointmentChecklistHistory>();
}
