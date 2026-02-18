using System.Globalization;
using System.Net;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;

namespace ConsertaPraMim.Web.Provider.Services;

public class ProviderApiServiceRequestService : IServiceRequestService
{
    private readonly ProviderApiCaller _apiCaller;

    public ProviderApiServiceRequestService(ProviderApiCaller apiCaller)
    {
        _apiCaller = apiCaller;
    }

    public async Task<Guid> CreateAsync(Guid clientId, CreateServiceRequestDto dto)
    {
        var response = await _apiCaller.SendAsync<CreateIdResponse>(HttpMethod.Post, "/api/service-requests", dto);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new UnauthorizedAccessException(response.ErrorMessage ?? "Sessao expirada. Faca login novamente.");
        }

        if (!response.Success || response.Payload == null || response.Payload.Id == Guid.Empty)
        {
            throw new InvalidOperationException(response.ErrorMessage ?? "Nao foi possivel criar o pedido.");
        }

        return response.Payload.Id;
    }

    public async Task<IEnumerable<ServiceRequestDto>> GetAllAsync(Guid userId, string role, string? searchTerm = null)
    {
        var path = "/api/service-requests";
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            path += $"?searchTerm={Uri.EscapeDataString(searchTerm.Trim())}";
        }

        var response = await _apiCaller.SendAsync<List<ServiceRequestDto>>(HttpMethod.Get, path);
        return response.Payload ?? [];
    }

    public async Task<IEnumerable<ProviderServiceMapPinDto>> GetMapPinsForProviderAsync(Guid providerId, double? maxDistanceKm = null, int take = 200)
    {
        var query = new List<string>
        {
            $"take={Math.Max(1, take)}"
        };

        if (maxDistanceKm.HasValue && maxDistanceKm.Value > 0)
        {
            query.Add($"maxDistanceKm={maxDistanceKm.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        var path = $"/api/service-requests/provider/map-pins?{string.Join("&", query)}";
        var response = await _apiCaller.SendAsync<List<ProviderServiceMapPinDto>>(HttpMethod.Get, path);
        return response.Payload ?? [];
    }

    public async Task<ServiceRequestDto?> GetByIdAsync(Guid id, Guid actorUserId, string actorRole)
    {
        var response = await _apiCaller.SendAsync<ServiceRequestDto>(HttpMethod.Get, $"/api/service-requests/{id}");
        return response.Payload;
    }

    public async Task<IEnumerable<ServiceRequestDto>> GetScheduledByProviderAsync(Guid providerId)
    {
        var response = await _apiCaller.SendAsync<List<ServiceRequestDto>>(HttpMethod.Get, "/api/service-requests/provider/scheduled");
        return response.Payload ?? [];
    }

    public async Task<IEnumerable<ServiceRequestDto>> GetHistoryByProviderAsync(Guid providerId)
    {
        var response = await _apiCaller.SendAsync<List<ServiceRequestDto>>(HttpMethod.Get, "/api/service-requests/provider/history");
        return response.Payload ?? [];
    }

    public Task<bool> CompleteAsync(Guid requestId, Guid providerId)
    {
        return Task.FromResult(false);
    }

    private sealed record CreateIdResponse(Guid Id);
}

