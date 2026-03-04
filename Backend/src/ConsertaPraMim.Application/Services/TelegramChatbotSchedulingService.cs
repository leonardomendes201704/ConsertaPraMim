using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public sealed class TelegramChatbotSchedulingService : ITelegramChatbotSchedulingService
{
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IUserRepository _userRepository;

    public TelegramChatbotSchedulingService(
        IServiceRequestRepository serviceRequestRepository,
        IUserRepository userRepository)
    {
        _serviceRequestRepository = serviceRequestRepository;
        _userRepository = userRepository;
    }

    public async Task<TelegramChatbotEligibleProvidersResultDto> GetEligibleProvidersAsync(
        Guid clientId,
        Guid serviceRequestId,
        int take = 5)
    {
        if (serviceRequestId == Guid.Empty)
        {
            return new TelegramChatbotEligibleProvidersResultDto(
                Success: false,
                ServiceRequestId: serviceRequestId,
                Providers: [],
                ErrorCode: "invalid_request",
                ErrorMessage: "Pedido invalido para busca de prestadores.");
        }

        var serviceRequest = await _serviceRequestRepository.GetByIdAsync(serviceRequestId);
        if (serviceRequest == null)
        {
            return new TelegramChatbotEligibleProvidersResultDto(
                Success: false,
                ServiceRequestId: serviceRequestId,
                Providers: [],
                ErrorCode: "request_not_found",
                ErrorMessage: "Pedido nao encontrado.");
        }

        if (serviceRequest.ClientId != clientId)
        {
            return new TelegramChatbotEligibleProvidersResultDto(
                Success: false,
                ServiceRequestId: serviceRequestId,
                Providers: [],
                ErrorCode: "forbidden",
                ErrorMessage: "Cliente sem acesso ao pedido informado.");
        }

        if (serviceRequest.Status is ServiceRequestStatus.Canceled or ServiceRequestStatus.Completed)
        {
            return new TelegramChatbotEligibleProvidersResultDto(
                Success: false,
                ServiceRequestId: serviceRequestId,
                Providers: [],
                ErrorCode: "request_closed",
                ErrorMessage: "Pedido encerrado para matching.");
        }

        var safeTake = Math.Clamp(take, 1, 10);
        var providers = await _userRepository.GetAllAsync();
        var clientProfileType = serviceRequest.Client?.ClientProfileType ?? ClientProfileType.Pf;

        var eligible = providers
            .Where(IsEligibleProvider)
            .Select(provider => BuildCandidate(provider, serviceRequest, clientProfileType))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .OrderBy(candidate => candidate.DistanceKm)
            .ThenByDescending(candidate => candidate.ProviderProfile.Rating)
            .Take(safeTake)
            .Select(candidate => new TelegramChatbotEligibleProviderDto(
                ProviderId: candidate.Provider.Id,
                ProviderName: string.IsNullOrWhiteSpace(candidate.Provider.Name)
                    ? "Prestador"
                    : candidate.Provider.Name.Trim(),
                DistanceKm: Math.Round(candidate.DistanceKm, 2, MidpointRounding.AwayFromZero),
                Rating: Math.Round(candidate.ProviderProfile.Rating, 2, MidpointRounding.AwayFromZero),
                ReviewCount: Math.Max(0, candidate.ProviderProfile.ReviewCount),
                CoverageRadiusKm: Math.Round(candidate.ProviderProfile.RadiusKm, 2, MidpointRounding.AwayFromZero),
                BaseZipCode: candidate.ProviderProfile.BaseZipCode,
                Categories: candidate.ProviderProfile.Categories))
            .ToList();

        return new TelegramChatbotEligibleProvidersResultDto(
            Success: true,
            ServiceRequestId: serviceRequestId,
            Providers: eligible);
    }

    private static bool IsEligibleProvider(User provider)
    {
        if (provider.Role != UserRole.Provider || !provider.IsActive)
        {
            return false;
        }

        var profile = provider.ProviderProfile;
        if (profile == null || !profile.BaseLatitude.HasValue || !profile.BaseLongitude.HasValue)
        {
            return false;
        }

        if (profile.RadiusKm <= 0)
        {
            return false;
        }

        if (profile.Categories == null || profile.Categories.Count == 0)
        {
            return false;
        }

        return true;
    }

    private static EligibleProviderCandidate? BuildCandidate(
        User provider,
        ServiceRequest serviceRequest,
        ClientProfileType clientProfileType)
    {
        var profile = provider.ProviderProfile;
        if (profile == null || !profile.BaseLatitude.HasValue || !profile.BaseLongitude.HasValue)
        {
            return null;
        }

        if (!profile.Categories.Contains(serviceRequest.Category))
        {
            return null;
        }

        if (!CanProviderAttendClientType(profile.ClientPreference, clientProfileType))
        {
            return null;
        }

        var distanceKm = CalculateDistanceKm(
            profile.BaseLatitude.Value,
            profile.BaseLongitude.Value,
            serviceRequest.Latitude,
            serviceRequest.Longitude);

        if (distanceKm > profile.RadiusKm)
        {
            return null;
        }

        return new EligibleProviderCandidate(provider, profile, distanceKm);
    }

    private static bool CanProviderAttendClientType(ProviderClientPreference preference, ClientProfileType clientProfileType)
    {
        return preference switch
        {
            ProviderClientPreference.PfOnly => clientProfileType == ClientProfileType.Pf,
            ProviderClientPreference.PjOnly => clientProfileType == ClientProfileType.Pj,
            _ => true
        };
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

    private sealed record EligibleProviderCandidate(
        User Provider,
        ProviderProfile ProviderProfile,
        double DistanceKm);
}
