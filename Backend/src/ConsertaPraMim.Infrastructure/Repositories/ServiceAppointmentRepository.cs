using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class ServiceAppointmentRepository : IServiceAppointmentRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public ServiceAppointmentRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ProviderAvailabilityRule>> GetAvailabilityRulesByProviderAsync(Guid providerId)
    {
        var rules = await _context.ProviderAvailabilityRules
            .AsNoTracking()
            .Where(r => r.ProviderId == providerId && r.IsActive)
            .ToListAsync();

        return rules
            .OrderBy(r => r.DayOfWeek)
            .ThenBy(r => r.StartTime)
            .ToList();
    }

    public async Task<IReadOnlyList<ProviderAvailabilityException>> GetAvailabilityExceptionsByProviderAsync(
        Guid providerId,
        DateTime rangeStartUtc,
        DateTime rangeEndUtc)
    {
        return await _context.ProviderAvailabilityExceptions
            .AsNoTracking()
            .Where(e => e.ProviderId == providerId && e.IsActive)
            .Where(e => e.StartsAtUtc < rangeEndUtc && e.EndsAtUtc > rangeStartUtc)
            .OrderBy(e => e.StartsAtUtc)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ProviderAvailabilityException>> GetAvailabilityExceptionsByProviderAsync(Guid providerId)
    {
        return await _context.ProviderAvailabilityExceptions
            .AsNoTracking()
            .Where(e => e.ProviderId == providerId && e.IsActive)
            .OrderBy(e => e.StartsAtUtc)
            .ToListAsync();
    }

    public async Task<ProviderAvailabilityRule?> GetAvailabilityRuleByIdAsync(Guid ruleId)
    {
        return await _context.ProviderAvailabilityRules
            .FirstOrDefaultAsync(r => r.Id == ruleId);
    }

    public async Task<ProviderAvailabilityException?> GetAvailabilityExceptionByIdAsync(Guid exceptionId)
    {
        return await _context.ProviderAvailabilityExceptions
            .FirstOrDefaultAsync(e => e.Id == exceptionId);
    }

    public async Task AddAvailabilityRuleAsync(ProviderAvailabilityRule rule)
    {
        await _context.ProviderAvailabilityRules.AddAsync(rule);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAvailabilityRuleAsync(ProviderAvailabilityRule rule)
    {
        _context.ProviderAvailabilityRules.Update(rule);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveAvailabilityRuleAsync(ProviderAvailabilityRule rule)
    {
        _context.ProviderAvailabilityRules.Remove(rule);
        await _context.SaveChangesAsync();
    }

    public async Task AddAvailabilityExceptionAsync(ProviderAvailabilityException exception)
    {
        await _context.ProviderAvailabilityExceptions.AddAsync(exception);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAvailabilityExceptionAsync(ProviderAvailabilityException exception)
    {
        _context.ProviderAvailabilityExceptions.Update(exception);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveAvailabilityExceptionAsync(ProviderAvailabilityException exception)
    {
        _context.ProviderAvailabilityExceptions.Remove(exception);
        await _context.SaveChangesAsync();
    }

    public async Task<ServiceAppointment?> GetByIdAsync(Guid appointmentId)
    {
        return await _context.ServiceAppointments
            .Include(a => a.ServiceRequest)
            .Include(a => a.Client)
            .Include(a => a.Provider)
            .Include(a => a.History)
                .ThenInclude(h => h.ActorUser)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);
    }

    public async Task<IReadOnlyList<ServiceAppointment>> GetByRequestIdAsync(Guid requestId)
    {
        return await _context.ServiceAppointments
            .AsNoTracking()
            .Include(a => a.ServiceRequest)
            .Include(a => a.Client)
            .Include(a => a.Provider)
            .Include(a => a.History)
                .ThenInclude(h => h.ActorUser)
            .Where(a => a.ServiceRequestId == requestId)
            .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ServiceAppointment>> GetByProviderAsync(Guid providerId, DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var query = _context.ServiceAppointments
            .AsNoTracking()
            .Include(a => a.ServiceRequest)
            .Include(a => a.Client)
            .Where(a => a.ProviderId == providerId);

        if (fromUtc.HasValue)
        {
            query = query.Where(a => a.WindowEndUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(a => a.WindowStartUtc <= toUtc.Value);
        }

        return await query
            .OrderBy(a => a.WindowStartUtc)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ServiceAppointment>> GetByClientAsync(Guid clientId, DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var query = _context.ServiceAppointments
            .AsNoTracking()
            .Include(a => a.ServiceRequest)
            .Include(a => a.Provider)
            .Where(a => a.ClientId == clientId);

        if (fromUtc.HasValue)
        {
            query = query.Where(a => a.WindowEndUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(a => a.WindowStartUtc <= toUtc.Value);
        }

        return await query
            .OrderBy(a => a.WindowStartUtc)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ServiceAppointment>> GetExpiredPendingAppointmentsAsync(DateTime asOfUtc, int take = 200)
    {
        var cappedTake = Math.Clamp(take, 1, 500);

        return await _context.ServiceAppointments
            .Include(a => a.ServiceRequest)
            .Include(a => a.Client)
            .Include(a => a.Provider)
            .Where(a => a.Status == ServiceAppointmentStatus.PendingProviderConfirmation)
            .Where(a => a.ExpiresAtUtc.HasValue && a.ExpiresAtUtc.Value <= asOfUtc)
            .OrderBy(a => a.ExpiresAtUtc)
            .Take(cappedTake)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ServiceAppointment>> GetProviderAppointmentsByStatusesInRangeAsync(
        Guid providerId,
        DateTime rangeStartUtc,
        DateTime rangeEndUtc,
        IReadOnlyCollection<ServiceAppointmentStatus> statuses)
    {
        if (statuses.Count == 0)
        {
            return Array.Empty<ServiceAppointment>();
        }

        return await _context.ServiceAppointments
            .AsNoTracking()
            .Where(a => a.ProviderId == providerId)
            .Where(a => statuses.Contains(a.Status))
            .Where(a => a.WindowStartUtc < rangeEndUtc && a.WindowEndUtc > rangeStartUtc)
            .OrderBy(a => a.WindowStartUtc)
            .ToListAsync();
    }

    public async Task AddAsync(ServiceAppointment appointment)
    {
        await _context.ServiceAppointments.AddAsync(appointment);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ServiceAppointment appointment)
    {
        _context.ServiceAppointments.Update(appointment);
        await _context.SaveChangesAsync();
    }

    public async Task AddHistoryAsync(ServiceAppointmentHistory history)
    {
        await _context.ServiceAppointmentHistories.AddAsync(history);
        await _context.SaveChangesAsync();
    }
}
