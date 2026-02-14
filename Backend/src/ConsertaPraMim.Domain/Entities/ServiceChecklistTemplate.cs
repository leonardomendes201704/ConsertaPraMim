using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class ServiceChecklistTemplate : BaseEntity
{
    public Guid CategoryDefinitionId { get; set; }
    public ServiceCategoryDefinition CategoryDefinition { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ServiceChecklistTemplateItem> Items { get; set; } = new List<ServiceChecklistTemplateItem>();
}
