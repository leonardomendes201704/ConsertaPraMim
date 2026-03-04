using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
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

    public async Task<IReadOnlyList<ServiceAppointmentCalendarSync>> GetRetryDueAsync(DateTime asOfUtc, int take)
    {
        var cappedTake = Math.Clamp(take, 1, 1000);
        return await _context.ServiceAppointmentCalendarSyncs
            .Include(x => x.Appointment)
                .ThenInclude(a => a.ServiceRequest)
            .Include(x => x.Appointment)
                .ThenInclude(a => a.Client)
            .Include(x => x.Appointment)
                .ThenInclude(a => a.Provider)
            .Where(x => x.SyncStatus == ServiceAppointmentCalendarSyncStatus.Failed)
            .Where(x => x.NextRetryAtUtc.HasValue && x.NextRetryAtUtc.Value <= asOfUtc)
            .Where(x => x.RetryCount < x.MaxRetryAttempts)
            .OrderBy(x => x.NextRetryAtUtc)
            .ThenBy(x => x.CreatedAt)
            .Take(cappedTake)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ServiceAppointmentCalendarSync>> QueryForReprocessAsync(
        Guid? appointmentId,
        DateTime? fromUtc,
        DateTime? toUtc,
        IReadOnlyCollection<ServiceAppointmentCalendarSyncStatus> statuses,
        int take)
    {
        var cappedTake = Math.Clamp(take, 1, 2000);
        var query = _context.ServiceAppointmentCalendarSyncs
            .Include(x => x.Appointment)
                .ThenInclude(a => a.ServiceRequest)
            .Include(x => x.Appointment)
                .ThenInclude(a => a.Client)
            .Include(x => x.Appointment)
                .ThenInclude(a => a.Provider)
            .AsQueryable();

        if (appointmentId.HasValue && appointmentId.Value != Guid.Empty)
        {
            query = query.Where(x => x.AppointmentId == appointmentId.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(x => (x.LastSyncAtUtc ?? x.CreatedAt) >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => (x.LastSyncAtUtc ?? x.CreatedAt) <= toUtc.Value);
        }

        if (statuses.Count > 0)
        {
            query = query.Where(x => statuses.Contains(x.SyncStatus));
        }

        return await query
            .OrderBy(x => x.LastSyncAtUtc ?? x.CreatedAt)
            .Take(cappedTake)
            .ToListAsync();
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
