using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Repositories;

public interface IAppointmentReminderDispatchRepository
{
    Task<IReadOnlyList<AppointmentReminderDispatch>> GetByAppointmentIdAsync(Guid appointmentId);
    Task<IReadOnlyList<AppointmentReminderDispatch>> GetDueAsync(DateTime asOfUtc, int take = 200);
    Task<IReadOnlyList<AppointmentReminderDispatch>> QueryAsync(
        Guid? appointmentId = null,
        AppointmentReminderDispatchStatus? status = null,
        AppointmentReminderChannel? channel = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int skip = 0,
        int take = 100);
    Task<int> CountAsync(
        Guid? appointmentId = null,
        AppointmentReminderDispatchStatus? status = null,
        AppointmentReminderChannel? channel = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null);
    Task AddAsync(AppointmentReminderDispatch reminder);
    Task AddRangeAsync(IReadOnlyCollection<AppointmentReminderDispatch> reminders);
    Task UpdateAsync(AppointmentReminderDispatch reminder);
    Task<int> CancelPendingByAppointmentAsync(Guid appointmentId, string reason, DateTime cancelledAtUtc);
    Task<int> RegisterPresenceResponseAsync(
        Guid appointmentId,
        Guid recipientUserId,
        bool confirmed,
        string? reason,
        DateTime respondedAtUtc);
}
