using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Repositories;

public interface IServiceRequestRepository
{
    Task AddAsync(ServiceRequest request);
    Task<IEnumerable<ServiceRequest>> GetByClientIdAsync(Guid clientId);
    Task<IEnumerable<ServiceRequest>> GetAllAsync(); // For providers (filtered by radius later)
    Task<IEnumerable<ServiceRequest>> GetMatchingForProviderAsync(double lat, double lng, double radiusKm, List<ServiceCategory> categories, string? searchTerm = null);
    Task<ServiceRequest?> GetByIdAsync(Guid id);
    Task UpdateAsync(ServiceRequest request);
    Task<IEnumerable<ServiceRequest>> GetScheduledByProviderAsync(Guid providerId);
    Task<IEnumerable<ServiceRequest>> GetHistoryByProviderAsync(Guid providerId);
}
