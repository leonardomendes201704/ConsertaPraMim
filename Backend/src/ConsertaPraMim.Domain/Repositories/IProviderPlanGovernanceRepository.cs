using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Repositories;

public interface IProviderPlanGovernanceRepository
{
    Task<IReadOnlyList<ProviderPlanSetting>> GetPlanSettingsAsync();
    Task<ProviderPlanSetting?> GetPlanSettingAsync(ProviderPlan plan);
    Task AddPlanSettingAsync(ProviderPlanSetting setting);
    Task UpdatePlanSettingAsync(ProviderPlanSetting setting);

    Task<IReadOnlyList<ProviderPlanPromotion>> GetPromotionsAsync(bool includeInactive = true);
    Task<ProviderPlanPromotion?> GetPromotionByIdAsync(Guid promotionId);
    Task AddPromotionAsync(ProviderPlanPromotion promotion);
    Task UpdatePromotionAsync(ProviderPlanPromotion promotion);

    Task<IReadOnlyList<ProviderPlanCoupon>> GetCouponsAsync(bool includeInactive = true);
    Task<ProviderPlanCoupon?> GetCouponByIdAsync(Guid couponId);
    Task<ProviderPlanCoupon?> GetCouponByCodeAsync(string normalizedCouponCode);
    Task AddCouponAsync(ProviderPlanCoupon coupon);
    Task UpdateCouponAsync(ProviderPlanCoupon coupon);

    Task<int> GetCouponGlobalUsageCountAsync(Guid couponId);
    Task<int> GetCouponUsageCountByProviderAsync(Guid couponId, Guid providerId);
    Task AddCouponRedemptionAsync(ProviderPlanCouponRedemption redemption);
}
