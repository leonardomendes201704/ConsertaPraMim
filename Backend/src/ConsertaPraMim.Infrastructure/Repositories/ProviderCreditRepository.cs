using System.Data;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class ProviderCreditRepository : IProviderCreditRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public ProviderCreditRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task<ProviderCreditWallet?> GetWalletAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        return await _context.ProviderCreditWallets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProviderId == providerId, cancellationToken);
    }

    public async Task<ProviderCreditWallet> EnsureWalletAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        var existing = await GetWalletAsync(providerId, cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        var wallet = new ProviderCreditWallet
        {
            ProviderId = providerId,
            CurrentBalance = 0m
        };

        try
        {
            await _context.ProviderCreditWallets.AddAsync(wallet, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return wallet;
        }
        catch (DbUpdateException)
        {
            // Another concurrent request created the wallet first.
            var concurrent = await GetWalletAsync(providerId, cancellationToken);
            if (concurrent != null)
            {
                return concurrent;
            }

            throw;
        }
    }

    public async Task<(IReadOnlyList<ProviderCreditLedgerEntry> Items, int TotalCount)> GetStatementAsync(
        Guid providerId,
        DateTime? fromUtc,
        DateTime? toUtc,
        ProviderCreditLedgerEntryType? entryType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

        var query = _context.ProviderCreditLedgerEntries
            .AsNoTracking()
            .Where(x => x.ProviderId == providerId);

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.EffectiveAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.EffectiveAtUtc <= toUtc.Value);
        }

        if (entryType.HasValue)
        {
            query = query.Where(x => x.EntryType == entryType.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.EffectiveAtUtc)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<ProviderCreditLedgerEntry> AppendEntryAsync(
        Guid providerId,
        Func<ProviderCreditWallet, ProviderCreditLedgerEntry> entryFactory,
        CancellationToken cancellationToken = default)
    {
        ProviderCreditLedgerEntry? createdEntry = null;

        var executionStrategy = _context.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var wallet = await _context.ProviderCreditWallets
                .FirstOrDefaultAsync(x => x.ProviderId == providerId, cancellationToken);

            if (wallet == null)
            {
                wallet = new ProviderCreditWallet
                {
                    ProviderId = providerId,
                    CurrentBalance = 0m
                };

                await _context.ProviderCreditWallets.AddAsync(wallet, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }

            createdEntry = entryFactory(wallet);
            if (createdEntry == null)
            {
                throw new InvalidOperationException("Lancamento de credito invalido.");
            }

            createdEntry.ProviderId = providerId;
            createdEntry.WalletId = wallet.Id;

            wallet.CurrentBalance = createdEntry.BalanceAfter;
            wallet.LastMovementAtUtc = createdEntry.EffectiveAtUtc;
            wallet.UpdatedAt = DateTime.UtcNow;

            await _context.ProviderCreditLedgerEntries.AddAsync(createdEntry, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        });

        return createdEntry ?? throw new InvalidOperationException("Nao foi possivel persistir o lancamento de credito.");
    }
}
