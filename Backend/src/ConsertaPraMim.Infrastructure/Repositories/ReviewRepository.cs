using Microsoft.EntityFrameworkCore;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
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

    public async Task<Review?> GetByIdAsync(Guid reviewId)
    {
        return await _context.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId);
    }

    public async Task<Review?> GetByRequestAndReviewerAsync(Guid requestId, Guid reviewerUserId)
    {
        return await _context.Reviews
            .FirstOrDefaultAsync(r => r.RequestId == requestId && r.ReviewerUserId == reviewerUserId);
    }

    public async Task<IEnumerable<Review>> GetByRevieweeAsync(Guid revieweeUserId, UserRole revieweeRole)
    {
        return await _context.Reviews
            .Where(r => r.RevieweeUserId == revieweeUserId && r.RevieweeRole == revieweeRole)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Review>> GetReportedAsync()
    {
        return await _context.Reviews
            .Where(r => r.ModerationStatus == ReviewModerationStatus.Reported)
            .OrderByDescending(r => r.ReportedAtUtc ?? r.CreatedAt)
            .ToListAsync();
    }

    public async Task UpdateAsync(Review review)
    {
        _context.Reviews.Update(review);
        await _context.SaveChangesAsync();
    }
}
