using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class ProviderTrustReviewRepository : IProviderTrustReviewRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public ProviderTrustReviewRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ProviderTrustReview review)
    {
        await _context.ProviderTrustReviews.AddAsync(review);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ProviderTrustReview>> GetByProviderUserIdAsync(Guid providerUserId, int take = 30)
    {
        var normalizedTake = Math.Clamp(take, 1, 200);
        return await _context.ProviderTrustReviews
            .Where(review => review.ProviderUserId == providerUserId)
            .OrderByDescending(review => review.ReviewedAtUtc)
            .ThenByDescending(review => review.CreatedAt)
            .Take(normalizedTake)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ProviderProfile>> GetQueueAsync(
        ProviderTrustStatus? trustStatusFilter = null,
        ProviderRiskLevel? riskLevelFilter = null,
        int take = 100)
    {
        var normalizedTake = Math.Clamp(take, 1, 300);
        var query = _context.ProviderProfiles
            .Include(profile => profile.User)
            .Include(profile => profile.OnboardingDocuments)
            .AsQueryable();

        if (trustStatusFilter.HasValue)
        {
            query = query.Where(profile => profile.TrustStatus == trustStatusFilter.Value);
        }

        if (riskLevelFilter.HasValue)
        {
            query = query.Where(profile => profile.RiskLevel == riskLevelFilter.Value);
        }

        return await query
            .OrderByDescending(profile => profile.RiskLevel)
            .ThenBy(profile => profile.TrustStatus)
            .ThenByDescending(profile => profile.TrustStatusUpdatedAtUtc ?? profile.UpdatedAt ?? profile.CreatedAt)
            .Take(normalizedTake)
            .ToListAsync();
    }
}
