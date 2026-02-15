using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ServiceCompletionTerm : BaseEntity
{
    public Guid ServiceRequestId { get; set; }
    public ServiceRequest ServiceRequest { get; set; } = null!;

    public Guid ServiceAppointmentId { get; set; }
    public ServiceAppointment ServiceAppointment { get; set; } = null!;

    public Guid ProviderId { get; set; }
    public User Provider { get; set; } = null!;

    public Guid ClientId { get; set; }
    public User Client { get; set; } = null!;

    public ServiceCompletionTermStatus Status { get; set; } = ServiceCompletionTermStatus.PendingClientAcceptance;
    public ServiceCompletionAcceptanceMethod? AcceptedWithMethod { get; set; }

    public string Summary { get; set; } = string.Empty;
    public string PayloadHashSha256 { get; set; } = string.Empty;
    public string? PayloadJson { get; set; }
    public string? MetadataJson { get; set; }

    public string? AcceptancePinHashSha256 { get; set; }
    public DateTime? AcceptancePinExpiresAtUtc { get; set; }
    public int AcceptancePinFailedAttempts { get; set; }

    public string? AcceptedSignatureName { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }

    public string? ContestReason { get; set; }
    public DateTime? ContestedAtUtc { get; set; }
    public DateTime? EscalatedAtUtc { get; set; }
}
