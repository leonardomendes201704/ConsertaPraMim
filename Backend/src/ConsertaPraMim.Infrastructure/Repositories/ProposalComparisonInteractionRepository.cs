using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class ProposalComparisonInteractionRepository : IProposalComparisonInteractionRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public ProposalComparisonInteractionRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ProposalComparisonInteraction interaction, CancellationToken cancellationToken = default)
    {
        await _context.Set<ProposalComparisonInteraction>().AddAsync(interaction, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<ProposalComparisonInteraction?> GetLatestByClientAndRequestAsync(
        Guid clientUserId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        return _context.Set<ProposalComparisonInteraction>()
            .AsNoTracking()
            .Where(item => item.ClientUserId == clientUserId && item.RequestId == requestId)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProposalComparisonInteraction>> GetByWindowAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<ProposalComparisonInteraction>()
            .AsNoTracking()
            .Where(item => item.CreatedAt >= fromUtc && item.CreatedAt <= toUtc)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}

