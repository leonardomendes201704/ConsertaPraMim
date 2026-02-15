using ConsertaPraMim.Domain.Entities;

namespace ConsertaPraMim.Domain.Repositories;

public interface IServiceCompletionTermRepository
{
    Task AddAsync(ServiceCompletionTerm term);
    Task UpdateAsync(ServiceCompletionTerm term);
    Task<ServiceCompletionTerm?> GetByIdAsync(Guid id);
    Task<ServiceCompletionTerm?> GetByAppointmentIdAsync(Guid appointmentId);
    Task<IReadOnlyList<ServiceCompletionTerm>> GetByRequestIdAsync(Guid requestId);
}
