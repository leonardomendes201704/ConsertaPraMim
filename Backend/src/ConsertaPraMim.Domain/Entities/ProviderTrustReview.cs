using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ProviderTrustReview : BaseEntity
{
    public Guid ProviderProfileId { get; set; }
    public ProviderProfile ProviderProfile { get; set; } = null!;

    public Guid ProviderUserId { get; set; }
    public User ProviderUser { get; set; } = null!;

    public ProviderTrustStatus PreviousTrustStatus { get; set; } = ProviderTrustStatus.Pending;
    public ProviderTrustStatus NewTrustStatus { get; set; } = ProviderTrustStatus.Pending;
    public ProviderRiskLevel PreviousRiskLevel { get; set; } = ProviderRiskLevel.Low;
    public ProviderRiskLevel NewRiskLevel { get; set; } = ProviderRiskLevel.Low;

    public string? DecisionReason { get; set; }
    public string? EvidenceSummary { get; set; }

    public Guid ReviewedByAdminUserId { get; set; }
    public string ReviewedByAdminEmail { get; set; } = string.Empty;
    public DateTime ReviewedAtUtc { get; set; } = DateTime.UtcNow;
}
