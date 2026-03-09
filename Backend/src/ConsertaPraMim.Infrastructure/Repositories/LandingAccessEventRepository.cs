using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public sealed class LandingAccessEventRepository : ILandingAccessEventRepository
{
    private readonly ConsertaPraMimDbContext _dbContext;

    public LandingAccessEventRepository(ConsertaPraMimDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(LandingAccessEvent accessEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accessEvent);

        await _dbContext.LandingAccessEvents.AddAsync(accessEvent, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LandingAccessEvent>> GetByPeriodAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        return await _dbContext.LandingAccessEvents
            .AsNoTracking()
            .Where(accessEvent => accessEvent.CreatedAt >= fromUtc && accessEvent.CreatedAt <= toUtc)
            .OrderByDescending(accessEvent => accessEvent.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<LandingAccessEvent?> GetBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        var normalizedSessionId = sessionId.Trim();
        return await _dbContext.LandingAccessEvents
            .AsNoTracking()
            .OrderByDescending(accessEvent => accessEvent.CreatedAt)
            .FirstOrDefaultAsync(accessEvent => accessEvent.SessionId == normalizedSessionId, cancellationToken);
    }
}
