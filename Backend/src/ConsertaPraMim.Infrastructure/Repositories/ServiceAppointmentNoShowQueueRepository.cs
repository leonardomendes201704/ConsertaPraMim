using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class ServiceAppointmentNoShowQueueRepository : IServiceAppointmentNoShowQueueRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public ServiceAppointmentNoShowQueueRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceAppointmentNoShowQueueItem?> GetByAppointmentIdAsync(Guid appointmentId)
    {
        return await _context.ServiceAppointmentNoShowQueueItems
            .Include(x => x.ServiceAppointment)
            .FirstOrDefaultAsync(x => x.ServiceAppointmentId == appointmentId);
    }

    public async Task<IReadOnlyList<ServiceAppointmentNoShowQueueItem>> GetByStatusAsync(ServiceAppointmentNoShowQueueStatus status, int take = 200)
    {
        var cappedTake = Math.Clamp(take, 1, 2000);
        return await _context.ServiceAppointmentNoShowQueueItems
            .AsNoTracking()
            .Include(x => x.ServiceAppointment)
            .Where(x => x.Status == status)
            .OrderByDescending(x => x.LastDetectedAtUtc)
            .Take(cappedTake)
            .ToListAsync();
    }

    public async Task AddAsync(ServiceAppointmentNoShowQueueItem item)
    {
        await _context.ServiceAppointmentNoShowQueueItems.AddAsync(item);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ServiceAppointmentNoShowQueueItem item)
    {
        _context.ServiceAppointmentNoShowQueueItems.Update(item);
        await _context.SaveChangesAsync();
    }
}
