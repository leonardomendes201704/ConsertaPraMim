using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class ProviderGalleryRepository : IProviderGalleryRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public ProviderGalleryRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ProviderGalleryAlbum>> GetAlbumsByProviderAsync(Guid providerId)
    {
        return await _context.ProviderGalleryAlbums
            .AsNoTracking()
            .Include(a => a.ServiceRequest)
            .Include(a => a.Items)
            .Where(a => a.ProviderId == providerId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ProviderGalleryItem>> GetItemsByProviderAsync(Guid providerId)
    {
        return await _context.ProviderGalleryItems
            .AsNoTracking()
            .Include(i => i.Provider)
            .Include(i => i.Album)
                .ThenInclude(a => a.ServiceRequest)
            .Include(i => i.ServiceRequest)
            .Where(i => i.ProviderId == providerId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ProviderGalleryItem>> GetItemsByServiceRequestAsync(Guid serviceRequestId)
    {
        return await _context.ProviderGalleryItems
            .AsNoTracking()
            .Include(i => i.Provider)
            .Include(i => i.Album)
            .Include(i => i.ServiceRequest)
            .Where(i => i.ServiceRequestId == serviceRequestId)
            .OrderBy(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ProviderGalleryItem>> GetOperationalEvidenceCleanupCandidatesAsync(DateTime olderThanUtc, int batchSize)
    {
        var effectiveBatchSize = Math.Clamp(batchSize, 1, 2000);
        return await _context.ProviderGalleryItems
            .AsNoTracking()
            .Include(i => i.ServiceRequest)
            .Where(i =>
                (i.EvidencePhase.HasValue || i.ServiceAppointmentId.HasValue) &&
                i.CreatedAt <= olderThanUtc)
            .OrderBy(i => i.CreatedAt)
            .Take(effectiveBatchSize)
            .ToListAsync();
    }

    public async Task<ProviderGalleryAlbum?> GetAlbumByIdAsync(Guid albumId)
    {
        return await _context.ProviderGalleryAlbums
            .Include(a => a.ServiceRequest)
            .FirstOrDefaultAsync(a => a.Id == albumId);
    }

    public async Task<ProviderGalleryAlbum?> GetServiceAlbumAsync(Guid providerId, Guid serviceRequestId)
    {
        return await _context.ProviderGalleryAlbums
            .Include(a => a.ServiceRequest)
            .FirstOrDefaultAsync(a =>
                a.ProviderId == providerId &&
                a.ServiceRequestId == serviceRequestId);
    }

    public async Task AddAlbumAsync(ProviderGalleryAlbum album)
    {
        await _context.ProviderGalleryAlbums.AddAsync(album);
        await _context.SaveChangesAsync();
    }

    public async Task AddItemAsync(ProviderGalleryItem item)
    {
        await _context.ProviderGalleryItems.AddAsync(item);
        await _context.SaveChangesAsync();
    }

    public async Task<ProviderGalleryItem?> GetItemByIdAsync(Guid itemId)
    {
        return await _context.ProviderGalleryItems
            .FirstOrDefaultAsync(i => i.Id == itemId);
    }

    public async Task DeleteItemAsync(ProviderGalleryItem item)
    {
        _context.ProviderGalleryItems.Remove(item);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteItemsAsync(IReadOnlyCollection<ProviderGalleryItem> items)
    {
        if (items == null || items.Count == 0)
        {
            return;
        }

        _context.ProviderGalleryItems.RemoveRange(items);
        await _context.SaveChangesAsync();
    }
}
