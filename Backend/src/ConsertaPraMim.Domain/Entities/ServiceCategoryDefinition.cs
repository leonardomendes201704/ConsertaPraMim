using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ServiceCategoryDefinition : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Icon { get; set; } = "build_circle";
    public ServiceCategory LegacyCategory { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ServiceRequest> Requests { get; set; } = new List<ServiceRequest>();
    public ICollection<ServiceChecklistTemplate> ChecklistTemplates { get; set; } = new List<ServiceChecklistTemplate>();
}
