using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ServiceDisputeCase : BaseEntity
{
    public Guid ServiceRequestId { get; set; }
    public ServiceRequest ServiceRequest { get; set; } = null!;

    public Guid ServiceAppointmentId { get; set; }
    public ServiceAppointment ServiceAppointment { get; set; } = null!;

    public Guid OpenedByUserId { get; set; }
    public User OpenedByUser { get; set; } = null!;

    public ServiceAppointmentActorRole OpenedByRole { get; set; }

    public Guid CounterpartyUserId { get; set; }
    public User CounterpartyUser { get; set; } = null!;

    public ServiceAppointmentActorRole CounterpartyRole { get; set; }

    public Guid? OwnedByAdminUserId { get; set; }
    public User? OwnedByAdminUser { get; set; }

    public DateTime? OwnedAtUtc { get; set; }

    public DisputeCaseType Type { get; set; } = DisputeCaseType.Other;
    public DisputeCasePriority Priority { get; set; } = DisputeCasePriority.Medium;
    public DisputeCaseStatus Status { get; set; } = DisputeCaseStatus.Open;

    public ServiceAppointmentActorRole? WaitingForRole { get; set; }

    public string ReasonCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime SlaDueAtUtc { get; set; }
    public DateTime LastInteractionAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAtUtc { get; set; }

    public string? ResolutionSummary { get; set; }
    public string? MetadataJson { get; set; }

    public ICollection<ServiceDisputeCaseMessage> Messages { get; set; } = new List<ServiceDisputeCaseMessage>();
    public ICollection<ServiceDisputeCaseAttachment> Attachments { get; set; } = new List<ServiceDisputeCaseAttachment>();
    public ICollection<ServiceDisputeCaseAuditEntry> AuditEntries { get; set; } = new List<ServiceDisputeCaseAuditEntry>();
}
