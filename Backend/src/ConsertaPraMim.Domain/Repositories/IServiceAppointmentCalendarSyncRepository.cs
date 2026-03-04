using ConsertaPraMim.Domain.Entities;

namespace ConsertaPraMim.Domain.Repositories;

public interface IServiceAppointmentCalendarSyncRepository
{
    Task<ServiceAppointmentCalendarSync?> GetByAppointmentIdAsync(Guid appointmentId);
    Task<ServiceAppointmentCalendarSync?> GetByGoogleEventIdAsync(string googleEventId);
    Task AddAsync(ServiceAppointmentCalendarSync sync);
    Task UpdateAsync(ServiceAppointmentCalendarSync sync);
}
