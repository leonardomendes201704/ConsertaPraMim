using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class ServiceAppointmentCalendarSyncRepository : IServiceAppointmentCalendarSyncRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public ServiceAppointmentCalendarSyncRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceAppointmentCalendarSync?> GetByAppointmentIdAsync(Guid appointmentId)
    {
        return await _context.ServiceAppointmentCalendarSyncs
            .Include(x => x.Appointment)
            .FirstOrDefaultAsync(x => x.AppointmentId == appointmentId);
    }

    public async Task<ServiceAppointmentCalendarSync?> GetByGoogleEventIdAsync(string googleEventId)
    {
        if (string.IsNullOrWhiteSpace(googleEventId))
        {
            return null;
        }

        var normalizedGoogleEventId = googleEventId.Trim();
        return await _context.ServiceAppointmentCalendarSyncs
            .Include(x => x.Appointment)
            .FirstOrDefaultAsync(x => x.GoogleEventId == normalizedGoogleEventId);
    }

    public async Task AddAsync(ServiceAppointmentCalendarSync sync)
    {
        await _context.ServiceAppointmentCalendarSyncs.AddAsync(sync);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ServiceAppointmentCalendarSync sync)
    {
        _context.ServiceAppointmentCalendarSyncs.Update(sync);
        await _context.SaveChangesAsync();
    }
}
