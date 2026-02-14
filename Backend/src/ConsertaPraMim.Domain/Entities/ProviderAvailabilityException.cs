using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class ProviderAvailabilityException : BaseEntity
{
    public Guid ProviderId { get; set; }
    public User Provider { get; set; } = null!;

    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public string? Reason { get; set; }

    public bool IsActive { get; set; } = true;
}
