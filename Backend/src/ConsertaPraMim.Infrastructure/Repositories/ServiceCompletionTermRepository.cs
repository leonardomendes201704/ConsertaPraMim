using Microsoft.EntityFrameworkCore;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class ServiceCompletionTermRepository : IServiceCompletionTermRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public ServiceCompletionTermRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ServiceCompletionTerm term)
    {
        await _context.ServiceCompletionTerms.AddAsync(term);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ServiceCompletionTerm term)
    {
        _context.ServiceCompletionTerms.Update(term);
        await _context.SaveChangesAsync();
    }

    public async Task<ServiceCompletionTerm?> GetByIdAsync(Guid id)
    {
        return await _context.ServiceCompletionTerms
            .Include(t => t.ServiceRequest)
            .Include(t => t.ServiceAppointment)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<ServiceCompletionTerm?> GetByAppointmentIdAsync(Guid appointmentId)
    {
        return await _context.ServiceCompletionTerms
            .Include(t => t.ServiceRequest)
            .Include(t => t.ServiceAppointment)
            .Where(t => t.ServiceAppointmentId == appointmentId)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<ServiceCompletionTerm>> GetByRequestIdAsync(Guid requestId)
    {
        return await _context.ServiceCompletionTerms
            .Include(t => t.ServiceAppointment)
            .Where(t => t.ServiceRequestId == requestId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }
}
