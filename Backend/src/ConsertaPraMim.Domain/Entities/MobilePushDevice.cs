using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class MobilePushDevice : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Token { get; set; } = string.Empty;
    public string Platform { get; set; } = "android";
    public string AppKind { get; set; } = "client";

    public string? DeviceId { get; set; }
    public string? DeviceModel { get; set; }
    public string? OsVersion { get; set; }
    public string? AppVersion { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime LastRegisteredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastDeliveredAtUtc { get; set; }
    public DateTime? LastFailureAtUtc { get; set; }
    public string? LastFailureReason { get; set; }
}
