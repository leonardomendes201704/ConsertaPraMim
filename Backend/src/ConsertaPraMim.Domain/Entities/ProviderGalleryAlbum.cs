using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class ProviderGalleryAlbum : BaseEntity
{
    public Guid ProviderId { get; set; }
    public User Provider { get; set; } = null!;

    public Guid? ServiceRequestId { get; set; }
    public ServiceRequest? ServiceRequest { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool IsServiceAlbum { get; set; }

    public ICollection<ProviderGalleryItem> Items { get; set; } = new List<ProviderGalleryItem>();
}
