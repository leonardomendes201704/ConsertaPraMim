using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ServiceFinancialPolicyRule : BaseEntity
{
    public string Name { get; set; } = "Regra financeira";
    public ServiceFinancialPolicyEventType EventType { get; set; }
    public int MinHoursBeforeWindowStart { get; set; }
    public int? MaxHoursBeforeWindowStart { get; set; }

    public decimal PenaltyPercent { get; set; }
    public decimal CounterpartyCompensationPercent { get; set; }
    public decimal PlatformRetainedPercent { get; set; }

    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 1;
    public string? Notes { get; set; }
}
