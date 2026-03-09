using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public sealed class LandingLeadRepository : ILandingLeadRepository
{
    private readonly ConsertaPraMimDbContext _dbContext;

    public LandingLeadRepository(ConsertaPraMimDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(LandingLead lead, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lead);

        await _dbContext.LandingLeads.AddAsync(lead, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LandingLead>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.LandingLeads
            .AsNoTracking()
            .OrderByDescending(lead => lead.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LandingLead>> GetByPeriodAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        return await _dbContext.LandingLeads
            .AsNoTracking()
            .Where(lead => lead.CreatedAt >= fromUtc && lead.CreatedAt <= toUtc)
            .OrderByDescending(lead => lead.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<LandingLead?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.LandingLeads
            .AsNoTracking()
            .FirstOrDefaultAsync(lead => lead.Id == id, cancellationToken);
    }

    public async Task<LandingLead?> GetBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        var normalizedSessionId = sessionId.Trim();
        return await _dbContext.LandingLeads
            .AsNoTracking()
            .OrderByDescending(lead => lead.CreatedAt)
            .FirstOrDefaultAsync(lead => lead.SessionId == normalizedSessionId, cancellationToken);
    }
}
