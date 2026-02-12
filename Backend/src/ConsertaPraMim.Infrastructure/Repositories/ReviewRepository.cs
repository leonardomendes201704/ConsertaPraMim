using Microsoft.EntityFrameworkCore;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class ReviewRepository : IReviewRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public ReviewRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Review review)
    {
        await _context.Reviews.AddAsync(review);
        await _context.SaveChangesAsync();
    }

    public async Task<Review?> GetByRequestIdAsync(Guid requestId)
    {
        return await _context.Reviews
            .FirstOrDefaultAsync(r => r.RequestId == requestId);
    }

    public async Task<IEnumerable<Review>> GetByProviderIdAsync(Guid providerId)
    {
        return await _context.Reviews
            .Where(r => r.ProviderId == providerId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }
}
