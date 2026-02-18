using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Web.Provider.Services;

public class ProviderApiProfileService : IProfileService
{
    private readonly ProviderApiCaller _apiCaller;

    public ProviderApiProfileService(ProviderApiCaller apiCaller)
    {
        _apiCaller = apiCaller;
    }

    public async Task<UserProfileDto?> GetProfileAsync(Guid userId)
    {
        var response = await _apiCaller.SendAsync<UserProfileDto>(HttpMethod.Get, $"/api/profile/{userId}");
        return response.Payload;
    }

    public async Task<bool> UpdateProviderProfileAsync(Guid userId, UpdateProviderProfileDto dto)
    {
        var response = await _apiCaller.SendAsync<object>(HttpMethod.Put, "/api/profile/provider", dto);
        return response.Success;
    }

    public async Task<bool> UpdateProviderOperationalStatusAsync(Guid userId, ProviderOperationalStatus status)
    {
        var response = await _apiCaller.SendAsync<object>(HttpMethod.Put, "/api/profile/provider/status", new UpdateProviderOperationalStatusDto(status));
        return response.Success;
    }

    public async Task<ProviderOperationalStatus?> GetProviderOperationalStatusAsync(Guid userId)
    {
        var response = await _apiCaller.SendAsync<ProviderStatusResponse>(HttpMethod.Get, $"/api/profile/provider/{userId}/status");
        if (response.Payload == null || !Enum.TryParse<ProviderOperationalStatus>(response.Payload.Status, true, out var parsed))
        {
            return null;
        }

        return parsed;
    }

    public async Task<bool> UpdateProfilePictureAsync(Guid userId, string imageUrl)
    {
        var response = await _apiCaller.SendAsync<object>(HttpMethod.Put, "/api/profile/picture", new UpdateProfilePictureDto(imageUrl));
        return response.Success;
    }

    private sealed record ProviderStatusResponse(Guid ProviderId, string Status);
}
