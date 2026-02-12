using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using System.Linq;

namespace ConsertaPraMim.Application.Services;

public class ServiceRequestService : IServiceRequestService
{
    private readonly IServiceRequestRepository _repository;
    private readonly IUserRepository _userRepository;

    public ServiceRequestService(IServiceRequestRepository repository, IUserRepository userRepository)
    {
        _repository = repository;
        _userRepository = userRepository;
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

    public async Task<IEnumerable<ServiceRequestDto>> GetAllAsync(Guid userId, string role, string? searchTerm = null)
    {
        IEnumerable<ServiceRequest> requests;
        
        if (role == "Client")
        {
            requests = await _repository.GetByClientIdAsync(userId);
            if (!string.IsNullOrEmpty(searchTerm))
                requests = requests.Where(r => r.Description.Contains(searchTerm));
        }
        else
        {
            // Provider: Match by radius and categories
            var provider = await _userRepository.GetByIdAsync(userId);
            var profile = provider?.ProviderProfile;

            if (profile != null && profile.BaseLatitude.HasValue && profile.BaseLongitude.HasValue)
            {
                requests = await _repository.GetMatchingForProviderAsync(
                    profile.BaseLatitude.Value, 
                    profile.BaseLongitude.Value, 
                    profile.RadiusKm, 
                    profile.Categories,
                    searchTerm);
            }
            else
            {
                // Fallback: If no profile/location set, show all created requests
                requests = await _repository.GetAllAsync();
                requests = requests.Where(r => r.Status == ServiceRequestStatus.Created);
                if (!string.IsNullOrEmpty(searchTerm))
                    requests = requests.Where(r => r.Description.Contains(searchTerm));
            }
        }

        return requests.Select(r => new ServiceRequestDto(
            r.Id, 
            r.Status.ToString(), 
            r.Category.ToString(), 
            r.Description, 
            r.CreatedAt,
            r.AddressStreet,
            r.AddressCity,
            r.AddressZip,
            r.Client?.Name,
            r.Client?.Phone,
            r.ImageUrl,
            r.Review?.Rating,
            r.Review?.Comment,
            r.Proposals.FirstOrDefault(p => p.Accepted)?.EstimatedValue
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
            r.AddressStreet,
            r.AddressCity,
            r.AddressZip,
            r.Client?.Name,
            r.Client?.Phone,
            r.ImageUrl,
            r.Review?.Rating,
            r.Review?.Comment,
            r.Proposals.FirstOrDefault(p => p.Accepted)?.EstimatedValue
        );
    }

    public async Task<IEnumerable<ServiceRequestDto>> GetScheduledByProviderAsync(Guid providerId)
    {
        var requests = await _repository.GetScheduledByProviderAsync(providerId);
        return requests.Select(r => new ServiceRequestDto(
            r.Id, 
            r.Status.ToString(), 
            r.Category.ToString(), 
            r.Description, 
            r.CreatedAt,
            r.AddressStreet,
            r.AddressCity,
            r.AddressZip,
            r.Client?.Name,
            r.Client?.Phone,
            r.ImageUrl,
            r.Review?.Rating,
            r.Review?.Comment,
            r.Proposals.FirstOrDefault(p => p.Accepted)?.EstimatedValue
        ));
    }

    public async Task<IEnumerable<ServiceRequestDto>> GetHistoryByProviderAsync(Guid providerId)
    {
        var requests = await _repository.GetHistoryByProviderAsync(providerId);
        return requests.Select(r => new ServiceRequestDto(
            r.Id, 
            r.Status.ToString(), 
            r.Category.ToString(), 
            r.Description, 
            r.CreatedAt,
            r.AddressStreet,
            r.AddressCity,
            r.AddressZip,
            r.Client?.Name,
            r.Client?.Phone,
            r.ImageUrl,
            r.Review?.Rating,
            r.Review?.Comment,
            r.Proposals.FirstOrDefault(p => p.Accepted)?.EstimatedValue
        ));
    }

    public async Task<bool> CompleteAsync(Guid requestId, Guid providerId)
    {
        var request = await _repository.GetByIdAsync(requestId);
        if (request == null) return false;

        // Security: Check if this provider has an accepted proposal for this request
        if (!request.Proposals.Any(p => p.ProviderId == providerId && p.Accepted))
            return false;

        request.Status = ServiceRequestStatus.Completed;
        await _repository.UpdateAsync(request);
        return true;
    }
}
