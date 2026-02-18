using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;

namespace ConsertaPraMim.Web.Provider.Services;

public class ProviderApiProviderGalleryService : IProviderGalleryService
{
    private readonly ProviderApiCaller _apiCaller;

    public ProviderApiProviderGalleryService(ProviderApiCaller apiCaller)
    {
        _apiCaller = apiCaller;
    }

    public async Task<ProviderGalleryOverviewDto> GetOverviewAsync(Guid providerId, ProviderGalleryFilterDto filter)
    {
        var query = new List<string>();
        if (filter.AlbumId.HasValue)
        {
            query.Add($"albumId={filter.AlbumId.Value}");
        }

        if (filter.ServiceRequestId.HasValue)
        {
            query.Add($"serviceRequestId={filter.ServiceRequestId.Value}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            query.Add($"category={Uri.EscapeDataString(filter.Category)}");
        }

        var path = "/api/provider-gallery";
        if (query.Count > 0)
        {
            path += "?" + string.Join("&", query);
        }

        var response = await _apiCaller.SendAsync<ProviderGalleryOverviewDto>(HttpMethod.Get, path);
        if (response.Success && response.Payload != null)
        {
            return response.Payload;
        }

        throw new InvalidOperationException(response.ErrorMessage ?? "Nao foi possivel carregar a galeria.");
    }

    public async Task<IReadOnlyList<ServiceRequestEvidenceTimelineItemDto>> GetEvidenceTimelineByServiceRequestAsync(
        Guid serviceRequestId,
        Guid? actorUserId = null,
        string? actorRole = null)
    {
        var response = await _apiCaller.SendAsync<List<ServiceRequestEvidenceTimelineItemDto>>(
            HttpMethod.Get,
            $"/api/service-requests/{serviceRequestId}/evidences");

        return response.Payload ?? [];
    }

    public Task<ProviderGalleryEvidenceCleanupResultDto> CleanupOldOperationalEvidencesAsync(
        int retentionDays,
        int batchSize,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Operacao administrativa nao suportada no portal prestador.");

    public async Task<ProviderGalleryAlbumDto> CreateAlbumAsync(Guid providerId, CreateProviderGalleryAlbumDto dto)
    {
        var response = await _apiCaller.SendAsync<ProviderGalleryAlbumDto>(HttpMethod.Post, "/api/provider-gallery/albums", dto);
        if (response.Success && response.Payload != null)
        {
            return response.Payload;
        }

        throw new InvalidOperationException(response.ErrorMessage ?? "Nao foi possivel criar o album.");
    }

    public async Task<ProviderGalleryItemDto> AddItemAsync(Guid providerId, CreateProviderGalleryItemDto dto)
    {
        var response = await _apiCaller.SendAsync<ProviderGalleryItemDto>(HttpMethod.Post, "/api/provider-gallery/items", dto);
        if (response.Success && response.Payload != null)
        {
            return response.Payload;
        }

        throw new InvalidOperationException(response.ErrorMessage ?? "Nao foi possivel adicionar item na galeria.");
    }

    public async Task<bool> DeleteItemAsync(Guid providerId, Guid itemId)
    {
        var response = await _apiCaller.SendAsync<object>(HttpMethod.Delete, $"/api/provider-gallery/items/{itemId}");
        return response.Success;
    }
}
