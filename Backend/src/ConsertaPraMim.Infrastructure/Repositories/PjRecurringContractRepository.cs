using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class PjRecurringContractRepository : IPjRecurringContractRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public PjRecurringContractRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PjRecurringContract>> ListByClientUserIdAsync(
        Guid clientUserId,
        CancellationToken cancellationToken = default)
    {
        return await _context.PjRecurringContracts
            .AsNoTracking()
            .Where(x => x.ClientUserId == clientUserId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PjRecurringContract>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.PjRecurringContracts
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<PjRecurringContract?> GetByIdAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        return await _context.PjRecurringContracts
            .FirstOrDefaultAsync(x => x.Id == contractId, cancellationToken);
    }

    public async Task AddAsync(PjRecurringContract contract, CancellationToken cancellationToken = default)
    {
        await _context.PjRecurringContracts.AddAsync(contract, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PjRecurringContract contract, CancellationToken cancellationToken = default)
    {
        _context.PjRecurringContracts.Update(contract);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
