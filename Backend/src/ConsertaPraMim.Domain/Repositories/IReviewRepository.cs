using ConsertaPraMim.Domain.Entities;

namespace ConsertaPraMim.Domain.Repositories;

public interface IReviewRepository
{
    Task AddAsync(Review review);
    Task<Review?> GetByRequestIdAsync(Guid requestId);
    Task<IEnumerable<Review>> GetByProviderIdAsync(Guid providerId);
}
