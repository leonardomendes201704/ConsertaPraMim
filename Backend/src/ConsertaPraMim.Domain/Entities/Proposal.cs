using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class Proposal : BaseEntity
{
    public Guid RequestId { get; set; }
    public ServiceRequest Request { get; set; } = null!;
    
    public Guid ProviderId { get; set; }
    public User Provider { get; set; } = null!;
    
    public decimal? EstimatedValue { get; set; }
    public bool Accepted { get; set; }
    public bool IsInvalidated { get; set; }
    public DateTime? InvalidatedAt { get; set; }
    public Guid? InvalidatedByAdminId { get; set; }
    public string? InvalidationReason { get; set; }
    public string? Message { get; set; }
}
