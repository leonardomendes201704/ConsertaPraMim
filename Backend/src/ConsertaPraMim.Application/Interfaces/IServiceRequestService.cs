using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IServiceRequestService
{
    Task<Guid> CreateAsync(Guid clientId, CreateServiceRequestDto dto);
    Task<IEnumerable<ServiceRequestDto>> GetAllAsync(Guid userId, string role, string? searchTerm = null);
    Task<ServiceRequestDto?> GetByIdAsync(Guid id, Guid actorUserId, string actorRole);
    Task<IEnumerable<ServiceRequestDto>> GetScheduledByProviderAsync(Guid providerId);
    Task<IEnumerable<ServiceRequestDto>> GetHistoryByProviderAsync(Guid providerId);
    Task<bool> CompleteAsync(Guid requestId, Guid providerId);
}
