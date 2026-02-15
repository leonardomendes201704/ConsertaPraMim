using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Repositories;

public interface IAppointmentReminderPreferenceRepository
{
    Task<IReadOnlyList<AppointmentReminderPreference>> GetByUserIdAsync(Guid userId);
    Task<AppointmentReminderPreference?> GetByUserAndChannelAsync(Guid userId, AppointmentReminderChannel channel);
    Task AddAsync(AppointmentReminderPreference preference);
    Task UpdateAsync(AppointmentReminderPreference preference);
}
