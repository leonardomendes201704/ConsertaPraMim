using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ProviderPlanCouponRedemption : BaseEntity
{
    public Guid CouponId { get; set; }
    public ProviderPlanCoupon Coupon { get; set; } = null!;

    public Guid ProviderId { get; set; }
    public ProviderPlan Plan { get; set; }
    public decimal DiscountApplied { get; set; }
    public DateTime AppliedAtUtc { get; set; }
}
