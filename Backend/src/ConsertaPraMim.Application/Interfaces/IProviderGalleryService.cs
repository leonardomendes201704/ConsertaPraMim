using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IProviderGalleryService
{
    Task<ProviderGalleryOverviewDto> GetOverviewAsync(Guid providerId, ProviderGalleryFilterDto filter);
    Task<IReadOnlyList<ServiceRequestEvidenceTimelineItemDto>> GetEvidenceTimelineByServiceRequestAsync(Guid serviceRequestId);
    Task<ProviderGalleryAlbumDto> CreateAlbumAsync(Guid providerId, CreateProviderGalleryAlbumDto dto);
    Task<ProviderGalleryItemDto> AddItemAsync(Guid providerId, CreateProviderGalleryItemDto dto);
    Task<bool> DeleteItemAsync(Guid providerId, Guid itemId);
}
