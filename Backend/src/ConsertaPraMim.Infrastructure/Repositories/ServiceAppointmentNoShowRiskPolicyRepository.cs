using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class ServiceAppointmentNoShowRiskPolicyRepository : IServiceAppointmentNoShowRiskPolicyRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public ServiceAppointmentNoShowRiskPolicyRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceAppointmentNoShowRiskPolicy?> GetActiveAsync()
    {
        return await _context.ServiceAppointmentNoShowRiskPolicies
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<ServiceAppointmentNoShowRiskPolicy>> GetAllAsync()
    {
        return await _context.ServiceAppointmentNoShowRiskPolicies
            .AsNoTracking()
            .OrderByDescending(p => p.IsActive)
            .ThenByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(ServiceAppointmentNoShowRiskPolicy policy)
    {
        await _context.ServiceAppointmentNoShowRiskPolicies.AddAsync(policy);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ServiceAppointmentNoShowRiskPolicy policy)
    {
        _context.ServiceAppointmentNoShowRiskPolicies.Update(policy);
        await _context.SaveChangesAsync();
    }
}
