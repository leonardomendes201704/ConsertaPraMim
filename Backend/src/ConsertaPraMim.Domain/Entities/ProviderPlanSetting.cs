using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ProviderPlanSetting : BaseEntity
{
    public ProviderPlan Plan { get; set; }
    public decimal MonthlyPrice { get; set; }
    public double MaxRadiusKm { get; set; }
    public int MaxAllowedCategories { get; set; }
    public List<ServiceCategory> AllowedCategories { get; set; } = new();
}
