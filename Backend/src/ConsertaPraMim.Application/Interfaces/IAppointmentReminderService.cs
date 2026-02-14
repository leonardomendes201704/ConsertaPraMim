using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAppointmentReminderService
{
    Task ScheduleForAppointmentAsync(Guid appointmentId, string triggerReason);
    Task CancelPendingForAppointmentAsync(Guid appointmentId, string reason);
    Task<int> ProcessDueRemindersAsync(int batchSize = 200, CancellationToken cancellationToken = default);
    Task<AppointmentReminderDispatchListResultDto> GetDispatchesAsync(AppointmentReminderDispatchQueryDto query);
}
