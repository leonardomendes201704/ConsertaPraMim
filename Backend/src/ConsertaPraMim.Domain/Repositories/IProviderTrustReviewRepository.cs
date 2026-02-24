using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Repositories;

public interface IProviderTrustReviewRepository
{
    Task AddAsync(ProviderTrustReview review);
    Task<IReadOnlyList<ProviderTrustReview>> GetByProviderUserIdAsync(Guid providerUserId, int take = 30);
    Task<IReadOnlyList<ProviderProfile>> GetQueueAsync(
        ProviderTrustStatus? trustStatusFilter = null,
        ProviderRiskLevel? riskLevelFilter = null,
        int take = 100);
}
