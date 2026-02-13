using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Repositories;

public interface IProviderCreditRepository
{
    Task<ProviderCreditWallet?> GetWalletAsync(Guid providerId, CancellationToken cancellationToken = default);
    Task<ProviderCreditWallet> EnsureWalletAsync(Guid providerId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ProviderCreditLedgerEntry> Items, int TotalCount)> GetStatementAsync(
        Guid providerId,
        DateTime? fromUtc,
        DateTime? toUtc,
        ProviderCreditLedgerEntryType? entryType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<ProviderCreditLedgerEntry> AppendEntryAsync(
        Guid providerId,
        Func<ProviderCreditWallet, ProviderCreditLedgerEntry> entryFactory,
        CancellationToken cancellationToken = default);
}
