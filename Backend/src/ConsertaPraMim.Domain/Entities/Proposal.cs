using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class Proposal : BaseEntity
{
    public Guid RequestId { get; set; }
    public ServiceRequest Request { get; set; } = null!;
    
    public Guid ProviderId { get; set; }
    public User Provider { get; set; } = null!;
    
    public decimal? EstimatedValue { get; set; }
    public int? EstimatedLeadTimeHours { get; set; }
    public int? WarrantyDays { get; set; }
    public bool Accepted { get; set; }
    public bool IsInvalidated { get; set; }
    public DateTime? InvalidatedAt { get; set; }
    public Guid? InvalidatedByAdminId { get; set; }
    public string? InvalidationReason { get; set; }
    public string? Message { get; set; }
    public decimal? QualityScore { get; set; }
    public decimal? QualityCompletenessScore { get; set; }
    public decimal? QualityClarityScore { get; set; }
    public decimal? QualityHistoryScore { get; set; }
    public decimal? QualityCommercialScore { get; set; }
    public DateTime? QualityCalculatedAtUtc { get; set; }
}
