using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ProviderCreditLedgerEntry : BaseEntity
{
    public Guid WalletId { get; set; }
    public ProviderCreditWallet Wallet { get; set; } = null!;

    public Guid ProviderId { get; set; }
    public ProviderCreditLedgerEntryType EntryType { get; set; }

    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }

    public DateTime EffectiveAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }

    public Guid? AdminUserId { get; set; }
    public string? AdminEmail { get; set; }
    public string? Metadata { get; set; }
}
