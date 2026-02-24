using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class PjRecurringContract : BaseEntity
{
    public Guid ClientUserId { get; set; }
    public User ClientUser { get; set; } = null!;

    public ClientPjType ClientPjType { get; set; }
    public ServiceCategory Category { get; set; }
    public ProviderClientPreference ProviderEligibility { get; set; } = ProviderClientPreference.PjOnly;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public PjRecurringCadence Cadence { get; set; } = PjRecurringCadence.Monthly;
    public PjRecurringContractStatus Status { get; set; } = PjRecurringContractStatus.Active;

    public decimal MonthlyAmount { get; set; }
    public int IncludedVisitsPerCycle { get; set; }
    public int ResponseSlaHours { get; set; }
    public int OperationalWindowStartMinute { get; set; }
    public int OperationalWindowEndMinute { get; set; }
    public int OperationalDaysMask { get; set; } = 62;

    public DateTime StartsAtUtc { get; set; }
    public DateTime NextRenewalAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }
    public DateTime? LastRenewedAtUtc { get; set; }
    public DateTime? LastPaymentAtUtc { get; set; }

    public bool AutoRenew { get; set; } = true;
    public string? CancellationReason { get; set; }
}
