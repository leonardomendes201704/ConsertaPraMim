using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ServiceWarrantyClaim : BaseEntity
{
    public Guid ServiceRequestId { get; set; }
    public ServiceRequest ServiceRequest { get; set; } = null!;

    public Guid ServiceAppointmentId { get; set; }
    public ServiceAppointment ServiceAppointment { get; set; } = null!;

    public Guid ClientId { get; set; }
    public User Client { get; set; } = null!;

    public Guid ProviderId { get; set; }
    public User Provider { get; set; } = null!;

    public Guid? RevisitAppointmentId { get; set; }
    public ServiceAppointment? RevisitAppointment { get; set; }

    public ServiceWarrantyClaimStatus Status { get; set; } = ServiceWarrantyClaimStatus.PendingProviderReview;

    public string IssueDescription { get; set; } = string.Empty;
    public string? ProviderResponseReason { get; set; }
    public string? AdminEscalationReason { get; set; }
    public string? MetadataJson { get; set; }

    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime WarrantyWindowEndsAtUtc { get; set; }
    public DateTime ProviderResponseDueAtUtc { get; set; }

    public DateTime? ProviderRespondedAtUtc { get; set; }
    public DateTime? EscalatedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
}
