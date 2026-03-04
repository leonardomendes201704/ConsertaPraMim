using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Repositories;

public interface IServiceAppointmentCalendarSyncRepository
{
    Task<ServiceAppointmentCalendarSync?> GetByAppointmentIdAsync(Guid appointmentId);
    Task<ServiceAppointmentCalendarSync?> GetByGoogleEventIdAsync(string googleEventId);
    Task<IReadOnlyList<ServiceAppointmentCalendarSync>> GetRetryDueAsync(DateTime asOfUtc, int take);
    Task<IReadOnlyList<ServiceAppointmentCalendarSync>> QueryForReprocessAsync(
        Guid? appointmentId,
        DateTime? fromUtc,
        DateTime? toUtc,
        IReadOnlyCollection<ServiceAppointmentCalendarSyncStatus> statuses,
        int take);
    Task AddAsync(ServiceAppointmentCalendarSync sync);
    Task UpdateAsync(ServiceAppointmentCalendarSync sync);
}
