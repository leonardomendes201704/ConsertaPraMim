using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Repositories;

public interface IReviewRepository
{
    Task AddAsync(Review review);
    Task<Review?> GetByRequestAndReviewerAsync(Guid requestId, Guid reviewerUserId);
    Task<IEnumerable<Review>> GetByRevieweeAsync(Guid revieweeUserId, UserRole revieweeRole);
}
