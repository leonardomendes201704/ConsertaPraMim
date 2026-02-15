using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using System.Linq;

namespace ConsertaPraMim.Application.Services;

public class ServiceRequestService : IServiceRequestService
{
    private const string InactiveCategoryMessage = "A categoria selecionada esta inativa ou indisponivel para novos pedidos.";

    private readonly IServiceRequestRepository _repository;
    private readonly IServiceCategoryRepository _serviceCategoryRepository;
    private readonly IUserRepository _userRepository;
    private readonly IZipGeocodingService _zipGeocodingService;
    private readonly INotificationService _notificationService;

    public ServiceRequestService(
        IServiceRequestRepository repository,
        IServiceCategoryRepository serviceCategoryRepository,
        IUserRepository userRepository,
        IZipGeocodingService zipGeocodingService,
        INotificationService notificationService)
    {
        _repository = repository;
        _serviceCategoryRepository = serviceCategoryRepository;
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

        var selectedCategory = await ResolveCategoryForCreationAsync(dto);
        if (selectedCategory == null)
        {
            throw new InvalidOperationException(InactiveCategoryMessage);
        }

        var request = new ServiceRequest
        {
            ClientId = clientId,
            Category = selectedCategory.LegacyCategory,
            CategoryDefinitionId = selectedCategory.Id,
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
                provider.Id.ToString("N"),
                "Novo Pedido Proximo a Voce!",
                $"Um novo pedido da categoria {selectedCategory.Name} foi criado perto da sua regiao.",
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

    public async Task<IEnumerable<ProviderServiceMapPinDto>> GetMapPinsForProviderAsync(Guid providerId, double? maxDistanceKm = null, int take = 200)
    {
        var provider = await _userRepository.GetByIdAsync(providerId);
        var profile = provider?.ProviderProfile;

        if (profile?.BaseLatitude is not double providerLat || profile.BaseLongitude is not double providerLng)
        {
            return Enumerable.Empty<ProviderServiceMapPinDto>();
        }

        var interestRadiusKm = profile.RadiusKm > 0 ? profile.RadiusKm : 5.0;
        var effectiveMaxDistanceKm = maxDistanceKm.HasValue && maxDistanceKm.Value > 0
            ? maxDistanceKm.Value
            : Math.Clamp(interestRadiusKm * 4, 40.0, 250.0);

        var cappedTake = Math.Clamp(take, 1, 500);
        var categories = profile.Categories ?? new List<ServiceCategory>();
        var openRequests = await _repository.GetOpenWithinRadiusAsync(providerLat, providerLng, effectiveMaxDistanceKm);

        return openRequests
            .Select(r =>
            {
                var distanceKm = CalculateDistanceKm(providerLat, providerLng, r.Latitude, r.Longitude);
                return new ProviderServiceMapPinDto(
                    r.Id,
                    ResolveCategoryDisplay(r),
                    r.Description,
                    r.AddressStreet,
                    r.AddressCity,
                    r.AddressZip,
                    r.CreatedAt,
                    r.Latitude,
                    r.Longitude,
                    distanceKm,
                    distanceKm <= interestRadiusKm,
                    categories.Contains(r.Category));
            })
            .Where(pin => pin.DistanceKm <= effectiveMaxDistanceKm)
            .OrderBy(pin => pin.DistanceKm)
            .Take(cappedTake)
            .ToList();
    }

    public async Task<ServiceRequestDto?> GetByIdAsync(Guid id, Guid actorUserId, string actorRole)
    {
        var r = await _repository.GetByIdAsync(id);
        if (r == null) return null;
        if (!await CanAccessRequestAsync(r, actorUserId, actorRole)) return null;

        double? distanceKm = null;
        if (IsProviderRole(actorRole))
        {
            var provider = await _userRepository.GetByIdAsync(actorUserId);
            var profile = provider?.ProviderProfile;
            if (profile?.BaseLatitude.HasValue == true && profile.BaseLongitude.HasValue)
            {
                distanceKm = CalculateDistanceKm(
                    profile.BaseLatitude.Value,
                    profile.BaseLongitude.Value,
                    r.Latitude,
                    r.Longitude);
            }
        }

        return MapToDto(r, distanceKm);
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

        request.Status = ServiceRequestStatus.PendingClientCompletionAcceptance;
        await _repository.UpdateAsync(request);
        return true;
    }

    private static ServiceRequestDto MapToDto(ServiceRequest request, double? distanceKm = null)
    {
        var acceptedProposalValue = request.Proposals.FirstOrDefault(p => p.Accepted)?.EstimatedValue;
        var effectiveEstimatedValue = request.CommercialCurrentValue ?? acceptedProposalValue;

        return new ServiceRequestDto(
            request.Id,
            request.Status.ToString(),
            ResolveCategoryDisplay(request),
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
            effectiveEstimatedValue,
            distanceKm,
            request.CommercialVersion,
            request.CommercialState.ToString(),
            request.CommercialBaseValue,
            request.CommercialCurrentValue,
            request.CommercialUpdatedAtUtc
        );
    }

    private async Task<bool> CanAccessRequestAsync(ServiceRequest request, Guid actorUserId, string actorRole)
    {
        if (IsAdminRole(actorRole))
        {
            return true;
        }

        if (IsClientRole(actorRole))
        {
            return request.ClientId == actorUserId;
        }

        if (!IsProviderRole(actorRole))
        {
            return false;
        }

        if (request.Proposals.Any(p => p.ProviderId == actorUserId))
        {
            return true;
        }

        if (await CanProviderAccessRequestAsync(request, actorUserId))
        {
            return true;
        }

        return await CanProviderAccessRequestFromMapCoverageAsync(request, actorUserId);
    }

    private async Task<bool> CanProviderAccessRequestAsync(ServiceRequest request, Guid providerUserId)
    {
        var provider = await _userRepository.GetByIdAsync(providerUserId);
        var profile = provider?.ProviderProfile;
        if (profile?.BaseLatitude is not double providerLat || profile.BaseLongitude is not double providerLng)
        {
            return false;
        }

        if (profile.Categories == null || !profile.Categories.Contains(request.Category))
        {
            return false;
        }

        if (request.Status != ServiceRequestStatus.Created && request.Status != ServiceRequestStatus.Matching)
        {
            return false;
        }

        var distanceKm = CalculateDistanceKm(
            providerLat,
            providerLng,
            request.Latitude,
            request.Longitude);

        return distanceKm <= profile.RadiusKm;
    }

    private async Task<bool> CanProviderAccessRequestFromMapCoverageAsync(ServiceRequest request, Guid providerUserId)
    {
        var provider = await _userRepository.GetByIdAsync(providerUserId);
        var profile = provider?.ProviderProfile;
        if (profile?.BaseLatitude is not double providerLat || profile.BaseLongitude is not double providerLng)
        {
            return false;
        }

        if (request.Status != ServiceRequestStatus.Created && request.Status != ServiceRequestStatus.Matching)
        {
            return false;
        }

        var interestRadiusKm = profile.RadiusKm > 0 ? profile.RadiusKm : 5.0;
        var mapSearchRadiusKm = Math.Clamp(interestRadiusKm * 4, 40.0, 250.0);

        var distanceKm = CalculateDistanceKm(
            providerLat,
            providerLng,
            request.Latitude,
            request.Longitude);

        return distanceKm <= mapSearchRadiusKm;
    }

    private static bool IsAdminRole(string role)
    {
        return role.Equals(UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsClientRole(string role)
    {
        return role.Equals(UserRole.Client.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProviderRole(string role)
    {
        return role.Equals(UserRole.Provider.ToString(), StringComparison.OrdinalIgnoreCase);
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

    private async Task<ServiceCategoryDefinition?> ResolveCategoryForCreationAsync(CreateServiceRequestDto dto)
    {
        if (dto.CategoryId.HasValue && dto.CategoryId.Value != Guid.Empty)
        {
            var fromId = await _serviceCategoryRepository.GetByIdAsync(dto.CategoryId.Value);
            if (fromId?.IsActive == true)
            {
                return fromId;
            }

            return null;
        }

        if (!dto.Category.HasValue)
        {
            return null;
        }

        return await _serviceCategoryRepository.GetFirstActiveByLegacyAsync(dto.Category.Value);
    }

    private static string ResolveCategoryDisplay(ServiceRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.CategoryDefinition?.Name))
        {
            return request.CategoryDefinition.Name;
        }

        return request.Category.ToPtBr();
    }
}
