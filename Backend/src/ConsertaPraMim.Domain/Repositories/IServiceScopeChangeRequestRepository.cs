using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Repositories;

public interface IServiceScopeChangeRequestRepository
{
    Task<IReadOnlyList<ServiceScopeChangeRequest>> GetByAppointmentIdAsync(Guid appointmentId);
    Task<ServiceScopeChangeRequest?> GetLatestByAppointmentIdAsync(Guid appointmentId);
    Task<ServiceScopeChangeRequest?> GetLatestByAppointmentIdAndStatusAsync(Guid appointmentId, ServiceScopeChangeRequestStatus status);
    Task AddAsync(ServiceScopeChangeRequest scopeChangeRequest);
    Task UpdateAsync(ServiceScopeChangeRequest scopeChangeRequest);
}
