using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;

namespace ConsertaPraMim.Web.Client.Services;

public class ClientApiProviderGalleryService : IProviderGalleryService
{
    private readonly ClientApiCaller _apiCaller;

    public ClientApiProviderGalleryService(ClientApiCaller apiCaller)
    {
        _apiCaller = apiCaller;
    }

    public Task<ProviderGalleryOverviewDto> GetOverviewAsync(Guid providerId, ProviderGalleryFilterDto filter) =>
        throw new NotSupportedException("Operacao nao suportada no portal cliente.");

    public async Task<IReadOnlyList<ServiceRequestEvidenceTimelineItemDto>> GetEvidenceTimelineByServiceRequestAsync(
        Guid serviceRequestId,
        Guid? actorUserId = null,
        string? actorRole = null)
    {
        var response = await _apiCaller.SendAsync<List<ServiceRequestEvidenceTimelineItemDto>>(HttpMethod.Get, $"/api/service-requests/{serviceRequestId}/evidences");
        return response.Payload ?? [];
    }

    public Task<ProviderGalleryEvidenceCleanupResultDto> CleanupOldOperationalEvidencesAsync(
        int retentionDays,
        int batchSize,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Operacao nao suportada no portal cliente.");

    public Task<ProviderGalleryAlbumDto> CreateAlbumAsync(Guid providerId, CreateProviderGalleryAlbumDto dto) =>
        throw new NotSupportedException("Operacao nao suportada no portal cliente.");

    public Task<ProviderGalleryItemDto> AddItemAsync(Guid providerId, CreateProviderGalleryItemDto dto) =>
        throw new NotSupportedException("Operacao nao suportada no portal cliente.");

    public Task<bool> DeleteItemAsync(Guid providerId, Guid itemId) =>
        throw new NotSupportedException("Operacao nao suportada no portal cliente.");
}
