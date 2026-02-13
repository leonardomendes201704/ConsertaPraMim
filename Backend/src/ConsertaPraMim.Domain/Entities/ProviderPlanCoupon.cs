using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ProviderPlanCoupon : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ProviderPlan? Plan { get; set; }
    public PricingDiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public int? MaxGlobalUses { get; set; }
    public int? MaxUsesPerProvider { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ProviderPlanCouponRedemption> Redemptions { get; set; } = new List<ProviderPlanCouponRedemption>();
}
