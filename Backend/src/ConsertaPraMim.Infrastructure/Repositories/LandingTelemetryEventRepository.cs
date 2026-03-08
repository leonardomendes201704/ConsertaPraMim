using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public sealed class LandingTelemetryEventRepository : ILandingTelemetryEventRepository
{
    private readonly ConsertaPraMimDbContext _dbContext;

    public LandingTelemetryEventRepository(ConsertaPraMimDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddRangeAsync(
        IReadOnlyCollection<LandingTelemetryEvent> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (events.Count == 0)
        {
            return;
        }

        await _dbContext.LandingTelemetryEvents.AddRangeAsync(events, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LandingTelemetryEvent>> GetByPeriodAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.LandingTelemetryEvents
            .AsNoTracking()
            .Where(item => item.OccurredAtUtc >= fromUtc && item.OccurredAtUtc <= toUtc)
            .OrderByDescending(item => item.OccurredAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LandingTelemetryEvent>> GetBySessionIdAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Array.Empty<LandingTelemetryEvent>();
        }

        var normalizedSessionId = sessionId.Trim();
        return await _dbContext.LandingTelemetryEvents
            .AsNoTracking()
            .Where(item => item.SessionId == normalizedSessionId)
            .OrderBy(item => item.OccurredAtUtc)
            .ToListAsync(cancellationToken);
    }
}
