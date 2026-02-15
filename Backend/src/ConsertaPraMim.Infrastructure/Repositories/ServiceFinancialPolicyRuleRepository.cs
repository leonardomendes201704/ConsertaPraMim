using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class ServiceFinancialPolicyRuleRepository : IServiceFinancialPolicyRuleRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public ServiceFinancialPolicyRuleRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ServiceFinancialPolicyRule>> GetActiveByEventTypeAsync(ServiceFinancialPolicyEventType eventType)
    {
        return await _context.ServiceFinancialPolicyRules
            .AsNoTracking()
            .Where(rule => rule.IsActive && rule.EventType == eventType)
            .OrderBy(rule => rule.Priority)
            .ThenByDescending(rule => rule.MinHoursBeforeWindowStart)
            .ThenBy(rule => rule.MaxHoursBeforeWindowStart ?? int.MaxValue)
            .ToListAsync();
    }
}
