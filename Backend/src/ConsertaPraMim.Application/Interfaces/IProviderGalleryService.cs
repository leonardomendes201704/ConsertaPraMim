using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IProviderGalleryService
{
    Task<ProviderGalleryOverviewDto> GetOverviewAsync(Guid providerId, ProviderGalleryFilterDto filter);
    Task<IReadOnlyList<ServiceRequestEvidenceTimelineItemDto>> GetEvidenceTimelineByServiceRequestAsync(
        Guid serviceRequestId,
        Guid? actorUserId = null,
        string? actorRole = null);
    Task<ProviderGalleryEvidenceCleanupResultDto> CleanupOldOperationalEvidencesAsync(
        int retentionDays,
        int batchSize,
        CancellationToken cancellationToken = default);
    Task<ProviderGalleryAlbumDto> CreateAlbumAsync(Guid providerId, CreateProviderGalleryAlbumDto dto);
    Task<ProviderGalleryItemDto> AddItemAsync(Guid providerId, CreateProviderGalleryItemDto dto);
    Task<bool> DeleteItemAsync(Guid providerId, Guid itemId);
}
