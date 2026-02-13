using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class ProviderPlanGovernanceRepository : IProviderPlanGovernanceRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public ProviderPlanGovernanceRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ProviderPlanSetting>> GetPlanSettingsAsync()
    {
        return await _context.ProviderPlanSettings
            .AsNoTracking()
            .OrderBy(x => x.Plan)
            .ToListAsync();
    }

    public async Task<ProviderPlanSetting?> GetPlanSettingAsync(ProviderPlan plan)
    {
        return await _context.ProviderPlanSettings
            .FirstOrDefaultAsync(x => x.Plan == plan);
    }

    public async Task AddPlanSettingAsync(ProviderPlanSetting setting)
    {
        await _context.ProviderPlanSettings.AddAsync(setting);
        await _context.SaveChangesAsync();
    }

    public async Task UpdatePlanSettingAsync(ProviderPlanSetting setting)
    {
        _context.ProviderPlanSettings.Update(setting);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ProviderPlanPromotion>> GetPromotionsAsync(bool includeInactive = true)
    {
        var query = _context.ProviderPlanPromotions.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderByDescending(x => x.StartsAtUtc)
            .ThenBy(x => x.Plan)
            .ToListAsync();
    }

    public async Task<ProviderPlanPromotion?> GetPromotionByIdAsync(Guid promotionId)
    {
        return await _context.ProviderPlanPromotions
            .FirstOrDefaultAsync(x => x.Id == promotionId);
    }

    public async Task AddPromotionAsync(ProviderPlanPromotion promotion)
    {
        await _context.ProviderPlanPromotions.AddAsync(promotion);
        await _context.SaveChangesAsync();
    }

    public async Task UpdatePromotionAsync(ProviderPlanPromotion promotion)
    {
        _context.ProviderPlanPromotions.Update(promotion);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ProviderPlanCoupon>> GetCouponsAsync(bool includeInactive = true)
    {
        var query = _context.ProviderPlanCoupons
            .AsNoTracking()
            .Include(x => x.Redemptions)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.Code)
            .ToListAsync();
    }

    public async Task<ProviderPlanCoupon?> GetCouponByIdAsync(Guid couponId)
    {
        return await _context.ProviderPlanCoupons
            .Include(x => x.Redemptions)
            .FirstOrDefaultAsync(x => x.Id == couponId);
    }

    public async Task<ProviderPlanCoupon?> GetCouponByCodeAsync(string normalizedCouponCode)
    {
        if (string.IsNullOrWhiteSpace(normalizedCouponCode))
        {
            return null;
        }

        return await _context.ProviderPlanCoupons
            .Include(x => x.Redemptions)
            .FirstOrDefaultAsync(x => x.Code == normalizedCouponCode);
    }

    public async Task AddCouponAsync(ProviderPlanCoupon coupon)
    {
        await _context.ProviderPlanCoupons.AddAsync(coupon);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateCouponAsync(ProviderPlanCoupon coupon)
    {
        _context.ProviderPlanCoupons.Update(coupon);
        await _context.SaveChangesAsync();
    }

    public async Task<int> GetCouponGlobalUsageCountAsync(Guid couponId)
    {
        return await _context.ProviderPlanCouponRedemptions
            .CountAsync(x => x.CouponId == couponId);
    }

    public async Task<int> GetCouponUsageCountByProviderAsync(Guid couponId, Guid providerId)
    {
        return await _context.ProviderPlanCouponRedemptions
            .CountAsync(x => x.CouponId == couponId && x.ProviderId == providerId);
    }

    public async Task AddCouponRedemptionAsync(ProviderPlanCouponRedemption redemption)
    {
        await _context.ProviderPlanCouponRedemptions.AddAsync(redemption);
        await _context.SaveChangesAsync();
    }
}
