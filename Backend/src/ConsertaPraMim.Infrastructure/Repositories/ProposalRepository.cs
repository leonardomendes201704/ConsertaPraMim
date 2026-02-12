using Microsoft.EntityFrameworkCore;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class ProposalRepository : IProposalRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public ProposalRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Proposal proposal)
    {
        await _context.Proposals.AddAsync(proposal);
        await _context.SaveChangesAsync();
    }

    public async Task<Proposal?> GetByIdAsync(Guid id)
    {
        return await _context.Proposals
            .Include(p => p.Request)
            .Include(p => p.Provider)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IEnumerable<Proposal>> GetByRequestIdAsync(Guid requestId)
    {
        return await _context.Proposals
            .Include(p => p.Provider)
            .Where(p => p.RequestId == requestId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Proposal>> GetByProviderIdAsync(Guid providerId)
    {
        return await _context.Proposals
            .Include(p => p.Request)
            .Where(p => p.ProviderId == providerId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task UpdateAsync(Proposal proposal)
    {
        _context.Proposals.Update(proposal);
        await _context.SaveChangesAsync();
    }
}
