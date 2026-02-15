using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Repositories;

public interface IServiceAppointmentNoShowQueueRepository
{
    Task<ServiceAppointmentNoShowQueueItem?> GetByAppointmentIdAsync(Guid appointmentId);
    Task<IReadOnlyList<ServiceAppointmentNoShowQueueItem>> GetByStatusAsync(ServiceAppointmentNoShowQueueStatus status, int take = 200);
    Task AddAsync(ServiceAppointmentNoShowQueueItem item);
    Task UpdateAsync(ServiceAppointmentNoShowQueueItem item);
}
