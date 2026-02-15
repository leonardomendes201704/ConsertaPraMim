using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class AppointmentReminderDispatchRepository : IAppointmentReminderDispatchRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public AppointmentReminderDispatchRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AppointmentReminderDispatch>> GetByAppointmentIdAsync(Guid appointmentId)
    {
        return await _context.AppointmentReminderDispatches
            .Where(r => r.ServiceAppointmentId == appointmentId)
            .OrderBy(r => r.ScheduledForUtc)
            .ThenBy(r => r.Channel)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<AppointmentReminderDispatch>> GetDueAsync(DateTime asOfUtc, int take = 200)
    {
        var cappedTake = Math.Clamp(take, 1, 2000);
        return await _context.AppointmentReminderDispatches
            .Include(r => r.RecipientUser)
            .Include(r => r.ServiceAppointment)
            .Where(r => r.Status == AppointmentReminderDispatchStatus.Pending || r.Status == AppointmentReminderDispatchStatus.FailedRetryable)
            .Where(r => r.NextAttemptAtUtc <= asOfUtc)
            .OrderBy(r => r.NextAttemptAtUtc)
            .ThenBy(r => r.CreatedAt)
            .Take(cappedTake)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<AppointmentReminderDispatch>> QueryAsync(
        Guid? appointmentId = null,
        AppointmentReminderDispatchStatus? status = null,
        AppointmentReminderChannel? channel = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int skip = 0,
        int take = 100)
    {
        var query = BuildQuery(appointmentId, status, channel, fromUtc, toUtc);
        var normalizedSkip = Math.Max(0, skip);
        var normalizedTake = Math.Clamp(take, 1, 500);

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip(normalizedSkip)
            .Take(normalizedTake)
            .ToListAsync();
    }

    public async Task<int> CountAsync(
        Guid? appointmentId = null,
        AppointmentReminderDispatchStatus? status = null,
        AppointmentReminderChannel? channel = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null)
    {
        return await BuildQuery(appointmentId, status, channel, fromUtc, toUtc).CountAsync();
    }

    public async Task AddAsync(AppointmentReminderDispatch reminder)
    {
        await _context.AppointmentReminderDispatches.AddAsync(reminder);
        await _context.SaveChangesAsync();
    }

    public async Task AddRangeAsync(IReadOnlyCollection<AppointmentReminderDispatch> reminders)
    {
        if (reminders.Count == 0)
        {
            return;
        }

        await _context.AppointmentReminderDispatches.AddRangeAsync(reminders);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(AppointmentReminderDispatch reminder)
    {
        _context.AppointmentReminderDispatches.Update(reminder);
        await _context.SaveChangesAsync();
    }

    public async Task<int> CancelPendingByAppointmentAsync(Guid appointmentId, string reason, DateTime cancelledAtUtc)
    {
        var pending = await _context.AppointmentReminderDispatches
            .Where(r => r.ServiceAppointmentId == appointmentId)
            .Where(r => r.Status == AppointmentReminderDispatchStatus.Pending || r.Status == AppointmentReminderDispatchStatus.FailedRetryable)
            .ToListAsync();

        if (pending.Count == 0)
        {
            return 0;
        }

        foreach (var reminder in pending)
        {
            reminder.Status = AppointmentReminderDispatchStatus.Cancelled;
            reminder.CancelledAtUtc = cancelledAtUtc;
            reminder.LastError = reason;
            reminder.UpdatedAt = cancelledAtUtc;
        }

        await _context.SaveChangesAsync();
        return pending.Count;
    }

    public async Task<int> RegisterPresenceResponseAsync(
        Guid appointmentId,
        Guid recipientUserId,
        bool confirmed,
        string? reason,
        DateTime respondedAtUtc)
    {
        var presenceDispatches = await _context.AppointmentReminderDispatches
            .Where(r => r.ServiceAppointmentId == appointmentId)
            .Where(r => r.RecipientUserId == recipientUserId)
            .Where(r => r.Status != AppointmentReminderDispatchStatus.Cancelled)
            .Where(r => r.EventKey.Contains(":presence"))
            .ToListAsync();

        if (presenceDispatches.Count == 0)
        {
            return 0;
        }

        foreach (var dispatch in presenceDispatches)
        {
            if (!dispatch.DeliveredAtUtc.HasValue && dispatch.SentAtUtc.HasValue)
            {
                dispatch.DeliveredAtUtc = dispatch.SentAtUtc;
            }

            dispatch.ResponseReceivedAtUtc = respondedAtUtc;
            dispatch.ResponseConfirmed = confirmed;
            dispatch.ResponseReason = string.IsNullOrWhiteSpace(reason)
                ? null
                : reason.Trim()[..Math.Min(500, reason.Trim().Length)];
            dispatch.UpdatedAt = respondedAtUtc;
        }

        await _context.SaveChangesAsync();
        return presenceDispatches.Count;
    }

    private IQueryable<AppointmentReminderDispatch> BuildQuery(
        Guid? appointmentId,
        AppointmentReminderDispatchStatus? status,
        AppointmentReminderChannel? channel,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        var query = _context.AppointmentReminderDispatches
            .AsNoTracking()
            .Include(r => r.RecipientUser)
            .Include(r => r.ServiceAppointment)
            .AsQueryable();

        if (appointmentId.HasValue)
        {
            query = query.Where(r => r.ServiceAppointmentId == appointmentId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        if (channel.HasValue)
        {
            query = query.Where(r => r.Channel == channel.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(r => r.CreatedAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(r => r.CreatedAt <= toUtc.Value);
        }

        return query;
    }
}
