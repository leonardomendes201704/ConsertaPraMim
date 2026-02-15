using ConsertaPraMim.Domain.Entities;

namespace ConsertaPraMim.Domain.Repositories;

public interface IProviderGalleryRepository
{
    Task<IReadOnlyList<ProviderGalleryAlbum>> GetAlbumsByProviderAsync(Guid providerId);
    Task<IReadOnlyList<ProviderGalleryItem>> GetItemsByProviderAsync(Guid providerId);
    Task<IReadOnlyList<ProviderGalleryItem>> GetItemsByServiceRequestAsync(Guid serviceRequestId);
    Task<ProviderGalleryAlbum?> GetAlbumByIdAsync(Guid albumId);
    Task<ProviderGalleryAlbum?> GetServiceAlbumAsync(Guid providerId, Guid serviceRequestId);
    Task AddAlbumAsync(ProviderGalleryAlbum album);
    Task AddItemAsync(ProviderGalleryItem item);
    Task<ProviderGalleryItem?> GetItemByIdAsync(Guid itemId);
    Task DeleteItemAsync(ProviderGalleryItem item);
}
