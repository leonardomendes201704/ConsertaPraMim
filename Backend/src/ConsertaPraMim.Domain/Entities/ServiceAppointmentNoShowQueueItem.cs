using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ServiceAppointmentNoShowQueueItem : BaseEntity
{
    public Guid ServiceAppointmentId { get; set; }
    public ServiceAppointment ServiceAppointment { get; set; } = null!;

    public ServiceAppointmentNoShowRiskLevel RiskLevel { get; set; } = ServiceAppointmentNoShowRiskLevel.Medium;
    public int Score { get; set; }
    public string ReasonsCsv { get; set; } = string.Empty;

    public ServiceAppointmentNoShowQueueStatus Status { get; set; } = ServiceAppointmentNoShowQueueStatus.Open;
    public DateTime FirstDetectedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastDetectedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAtUtc { get; set; }
    public Guid? ResolvedByAdminUserId { get; set; }
    public User? ResolvedByAdminUser { get; set; }
    public string? ResolutionNote { get; set; }
}
