using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class AdminAuditLog : BaseEntity
{
    public Guid ActorUserId { get; set; }
    public string ActorEmail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public Guid? TargetId { get; set; }
    public string? Metadata { get; set; }
}
