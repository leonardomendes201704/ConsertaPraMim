using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class ProviderGalleryItem : BaseEntity
{
    public Guid ProviderId { get; set; }
    public User Provider { get; set; } = null!;

    public Guid AlbumId { get; set; }
    public ProviderGalleryAlbum Album { get; set; } = null!;

    public Guid? ServiceRequestId { get; set; }
    public ServiceRequest? ServiceRequest { get; set; }

    public string FileUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string MediaKind { get; set; } = string.Empty;

    public string? Category { get; set; }
    public string? Caption { get; set; }
}
