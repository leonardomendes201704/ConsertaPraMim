using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class Review : BaseEntity
{
    public Guid RequestId { get; set; }
    public ServiceRequest Request { get; set; } = null!;
    
    public Guid ClientId { get; set; }
    public Guid ProviderId { get; set; }

    public Guid ReviewerUserId { get; set; }
    public UserRole ReviewerRole { get; set; }
    public Guid RevieweeUserId { get; set; }
    public UserRole RevieweeRole { get; set; }
    
    public int Rating { get; set; } // 1-5
    public string Comment { get; set; } = string.Empty;

    public ReviewModerationStatus ModerationStatus { get; set; } = ReviewModerationStatus.None;
    public string? ReportReason { get; set; }
    public Guid? ReportedByUserId { get; set; }
    public DateTime? ReportedAtUtc { get; set; }
    public Guid? ModeratedByAdminId { get; set; }
    public string? ModerationReason { get; set; }
    public DateTime? ModeratedAtUtc { get; set; }
}
