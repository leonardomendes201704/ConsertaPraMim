using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ProviderPlanPromotion : BaseEntity
{
    public ProviderPlan Plan { get; set; }
    public string Name { get; set; } = string.Empty;
    public PricingDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
}
