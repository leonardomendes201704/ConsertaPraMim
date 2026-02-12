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
    public string? Message { get; set; }
}
