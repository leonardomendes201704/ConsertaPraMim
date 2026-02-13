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
    private readonly IZipGeocodingService _zipGeocodingService;
    private readonly INotificationService _notificationService;

    public ServiceRequestService(
        IServiceRequestRepository repository,
        IUserRepository userRepository,
        IZipGeocodingService zipGeocodingService,
        INotificationService notificationService)
    {
        _repository = repository;
        _userRepository = userRepository;
        _zipGeocodingService = zipGeocodingService;
        _notificationService = notificationService;
    }

    public async Task<Guid> CreateAsync(Guid clientId, CreateServiceRequestDto dto)
    {
        var resolvedCoordinates = await _zipGeocodingService.ResolveCoordinatesAsync(dto.Zip, dto.Street, dto.City);
        if (!resolvedCoordinates.HasValue)
        {
            throw new InvalidOperationException("Nao foi possivel localizar o CEP informado para o pedido.");
        }

        var request = new ServiceRequest
        {
            ClientId = clientId,
            Category = dto.Category,
            Description = dto.Description,
            AddressStreet = !string.IsNullOrWhiteSpace(dto.Street) ? dto.Street : (resolvedCoordinates.Value.Street ?? "Endereco nao informado"),
            AddressCity = !string.IsNullOrWhiteSpace(dto.City) ? dto.City : (resolvedCoordinates.Value.City ?? "Cidade nao informada"),
            AddressZip = resolvedCoordinates.Value.NormalizedZip,
            Latitude = resolvedCoordinates.Value.Latitude,
            Longitude = resolvedCoordinates.Value.Longitude,
            Status = ServiceRequestStatus.Created
        };

        await _repository.AddAsync(request);

        var users = await _userRepository.GetAllAsync();
        var matchingProviders = users.Where(u =>
            u.Role == UserRole.Provider &&
            u.IsActive &&
            !string.IsNullOrWhiteSpace(u.Email) &&
            u.ProviderProfile != null &&
            u.ProviderProfile.BaseLatitude.HasValue &&
            u.ProviderProfile.BaseLongitude.HasValue &&
            u.ProviderProfile.Categories != null &&
            u.ProviderProfile.Categories.Contains(request.Category) &&
            CalculateDistanceKm(
                u.ProviderProfile.BaseLatitude.Value,
                u.ProviderProfile.BaseLongitude.Value,
                request.Latitude,
                request.Longitude) <= u.ProviderProfile.RadiusKm);

        var notifyTasks = matchingProviders.Select(provider =>
            _notificationService.SendNotificationAsync(
                provider.Email,
                "Novo Pedido Proximo a Voce!",
                $"Um novo pedido da categoria {request.Category} foi criado perto da sua regiao.",
                $"/ServiceRequests/Details/{request.Id}"));

        await Task.WhenAll(notifyTasks);

        return request.Id;
    }

    public async Task<IEnumerable<ServiceRequestDto>> GetAllAsync(Guid userId, string role, string? searchTerm = null)
    {
        IEnumerable<ServiceRequest> requests;
        double? providerLat = null;
        double? providerLng = null;
        
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
                providerLat = profile.BaseLatitude.Value;
                providerLng = profile.BaseLongitude.Value;
                requests = await _repository.GetMatchingForProviderAsync(
                    providerLat.Value,
                    providerLng.Value,
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

        return requests.Select(r =>
        {
            var distanceKm = providerLat.HasValue && providerLng.HasValue
                ? (double?)CalculateDistanceKm(providerLat.Value, providerLng.Value, r.Latitude, r.Longitude)
                : null;

            return MapToDto(r, distanceKm);
        });
    }

    public async Task<ServiceRequestDto?> GetByIdAsync(Guid id)
    {
        var r = await _repository.GetByIdAsync(id);
        if (r == null) return null;

        return MapToDto(r);
    }

    public async Task<IEnumerable<ServiceRequestDto>> GetScheduledByProviderAsync(Guid providerId)
    {
        var requests = await _repository.GetScheduledByProviderAsync(providerId);
        return requests.Select(r => MapToDto(r));
    }

    public async Task<IEnumerable<ServiceRequestDto>> GetHistoryByProviderAsync(Guid providerId)
    {
        var requests = await _repository.GetHistoryByProviderAsync(providerId);
        return requests.Select(r => MapToDto(r));
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

    private static ServiceRequestDto MapToDto(ServiceRequest request, double? distanceKm = null)
    {
        return new ServiceRequestDto(
            request.Id,
            request.Status.ToString(),
            request.Category.ToString(),
            request.Description,
            request.CreatedAt,
            request.AddressStreet,
            request.AddressCity,
            request.AddressZip,
            request.Client?.Name,
            request.Client?.Phone,
            request.ImageUrl,
            request.Review?.Rating,
            request.Review?.Comment,
            request.Proposals.FirstOrDefault(p => p.Accepted)?.EstimatedValue,
            distanceKm
        );
    }

    private static double CalculateDistanceKm(double fromLat, double fromLng, double toLat, double toLng)
    {
        const double earthRadiusKm = 6371.0;

        var dLat = DegreesToRadians(toLat - fromLat);
        var dLng = DegreesToRadians(toLng - fromLng);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(fromLat)) * Math.Cos(DegreesToRadians(toLat)) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }
}
