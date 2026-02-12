using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.Services;

public class ServiceRequestService : IServiceRequestService
{
    private readonly IServiceRequestRepository _repository;

    public ServiceRequestService(IServiceRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> CreateAsync(Guid clientId, CreateServiceRequestDto dto)
    {
        var request = new ServiceRequest
        {
            ClientId = clientId,
            Category = dto.Category,
            Description = dto.Description,
            AddressStreet = dto.Street,
            AddressCity = dto.City,
            AddressZip = dto.Zip,
            Latitude = dto.Lat,
            Longitude = dto.Lng,
            Status = ServiceRequestStatus.Created
        };

        await _repository.AddAsync(request);
        return request.Id;
    }

    public async Task<IEnumerable<ServiceRequestDto>> GetAllAsync(Guid userId, string role)
    {
        IEnumerable<ServiceRequest> requests;
        
        if (role == "Client")
        {
            requests = await _repository.GetByClientIdAsync(userId);
        }
        else
        {
            // Provider sees all (filtered by radius in V2)
            requests = await _repository.GetAllAsync();
        }

        return requests.Select(r => new ServiceRequestDto(
            r.Id, 
            r.Status.ToString(), 
            r.Category.ToString(), 
            r.Description, 
            r.CreatedAt,
            r.AddressCity
        ));
    }

    public async Task<ServiceRequestDto?> GetByIdAsync(Guid id)
    {
        var r = await _repository.GetByIdAsync(id);
        if (r == null) return null;

        return new ServiceRequestDto(
            r.Id, 
            r.Status.ToString(), 
            r.Category.ToString(), 
            r.Description, 
            r.CreatedAt,
            r.AddressCity
        );
    }
}
