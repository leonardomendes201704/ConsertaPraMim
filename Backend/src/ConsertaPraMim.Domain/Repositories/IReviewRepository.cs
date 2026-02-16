using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Repositories;

public interface IReviewRepository
{
    Task AddAsync(Review review);
    Task<Review?> GetByIdAsync(Guid reviewId);
    Task<Review?> GetByRequestAndReviewerAsync(Guid requestId, Guid reviewerUserId);
    Task<IEnumerable<Review>> GetByRevieweeAsync(Guid revieweeUserId, UserRole revieweeRole);
    Task<IEnumerable<Review>> GetReportedAsync();
    Task UpdateAsync(Review review);
}
