using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Repositories;

public interface IServiceFinancialPolicyRuleRepository
{
    Task<IReadOnlyList<ServiceFinancialPolicyRule>> GetActiveByEventTypeAsync(ServiceFinancialPolicyEventType eventType);
}
