using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class ProviderCreditWallet : BaseEntity
{
    public Guid ProviderId { get; set; }
    public User Provider { get; set; } = null!;

    public decimal CurrentBalance { get; set; }
    public DateTime? LastMovementAtUtc { get; set; }

    public ICollection<ProviderCreditLedgerEntry> Entries { get; set; } = new List<ProviderCreditLedgerEntry>();
}
