using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IServiceRequestService
{
    Task<Guid> CreateAsync(Guid clientId, CreateServiceRequestDto dto);
    Task<IEnumerable<ServiceRequestDto>> GetAllAsync(Guid userId, string role);
    Task<ServiceRequestDto?> GetByIdAsync(Guid id);
}
