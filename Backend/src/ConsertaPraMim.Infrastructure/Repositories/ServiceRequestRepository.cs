using Microsoft.EntityFrameworkCore;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class ServiceRequestRepository : IServiceRequestRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public ServiceRequestRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ServiceRequest request)
    {
        await _context.ServiceRequests.AddAsync(request);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<ServiceRequest>> GetByClientIdAsync(Guid clientId)
    {
        return await _context.ServiceRequests
            .Include(r => r.Proposals)
            .Where(r => r.ClientId == clientId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ServiceRequest>> GetAllAsync()
    {
        return await _context.ServiceRequests
            .Include(r => r.Client)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<ServiceRequest?> GetByIdAsync(Guid id)
    {
        return await _context.ServiceRequests
            .Include(r => r.Client)
            .Include(r => r.Proposals)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task UpdateAsync(ServiceRequest request)
    {
        _context.ServiceRequests.Update(request);
        await _context.SaveChangesAsync();
    }
}
