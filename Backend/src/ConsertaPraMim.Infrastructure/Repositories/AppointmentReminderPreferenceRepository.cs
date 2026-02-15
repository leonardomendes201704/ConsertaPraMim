using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class AppointmentReminderPreferenceRepository : IAppointmentReminderPreferenceRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public AppointmentReminderPreferenceRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AppointmentReminderPreference>> GetByUserIdAsync(Guid userId)
    {
        return await _context.AppointmentReminderPreferences
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.Channel)
            .ToListAsync();
    }

    public async Task<AppointmentReminderPreference?> GetByUserAndChannelAsync(Guid userId, AppointmentReminderChannel channel)
    {
        return await _context.AppointmentReminderPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Channel == channel);
    }

    public async Task AddAsync(AppointmentReminderPreference preference)
    {
        await _context.AppointmentReminderPreferences.AddAsync(preference);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(AppointmentReminderPreference preference)
    {
        _context.AppointmentReminderPreferences.Update(preference);
        await _context.SaveChangesAsync();
    }
}
