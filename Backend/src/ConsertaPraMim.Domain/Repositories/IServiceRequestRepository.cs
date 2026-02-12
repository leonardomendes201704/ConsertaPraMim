using ConsertaPraMim.Domain.Entities;

namespace ConsertaPraMim.Domain.Repositories;

public interface IServiceRequestRepository
{
    Task AddAsync(ServiceRequest request);
    Task<IEnumerable<ServiceRequest>> GetByClientIdAsync(Guid clientId);
    Task<IEnumerable<ServiceRequest>> GetAllAsync(); // For providers (filtered by radius later)
    Task<ServiceRequest?> GetByIdAsync(Guid id);
    Task UpdateAsync(ServiceRequest request);
}
