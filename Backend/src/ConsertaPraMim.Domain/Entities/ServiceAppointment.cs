using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ServiceAppointment : BaseEntity
{
    public Guid ServiceRequestId { get; set; }
    public ServiceRequest ServiceRequest { get; set; } = null!;

    public Guid ClientId { get; set; }
    public User Client { get; set; } = null!;

    public Guid ProviderId { get; set; }
    public User Provider { get; set; } = null!;

    public DateTime WindowStartUtc { get; set; }
    public DateTime WindowEndUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? ProposedWindowStartUtc { get; set; }
    public DateTime? ProposedWindowEndUtc { get; set; }
    public DateTime? RescheduleRequestedAtUtc { get; set; }
    public ServiceAppointmentActorRole? RescheduleRequestedByRole { get; set; }
    public string? RescheduleRequestReason { get; set; }

    public ServiceAppointmentStatus Status { get; set; } = ServiceAppointmentStatus.PendingProviderConfirmation;
    public ServiceAppointmentOperationalStatus? OperationalStatus { get; set; }
    public DateTime? OperationalStatusUpdatedAtUtc { get; set; }
    public string? OperationalStatusReason { get; set; }
    public string? Reason { get; set; }

    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime? ArrivedAtUtc { get; set; }
    public double? ArrivedLatitude { get; set; }
    public double? ArrivedLongitude { get; set; }
    public double? ArrivedAccuracyMeters { get; set; }
    public string? ArrivedManualReason { get; set; }
    public bool? ClientPresenceConfirmed { get; set; }
    public DateTime? ClientPresenceRespondedAtUtc { get; set; }
    public string? ClientPresenceReason { get; set; }
    public bool? ProviderPresenceConfirmed { get; set; }
    public DateTime? ProviderPresenceRespondedAtUtc { get; set; }
    public string? ProviderPresenceReason { get; set; }
    public int? NoShowRiskScore { get; set; }
    public ServiceAppointmentNoShowRiskLevel? NoShowRiskLevel { get; set; }
    public string? NoShowRiskReasons { get; set; }
    public DateTime? NoShowRiskCalculatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public ICollection<ServiceAppointmentHistory> History { get; set; } = new List<ServiceAppointmentHistory>();
    public ICollection<ServiceAppointmentChecklistResponse> ChecklistResponses { get; set; } = new List<ServiceAppointmentChecklistResponse>();
    public ICollection<ServiceAppointmentChecklistHistory> ChecklistHistory { get; set; } = new List<ServiceAppointmentChecklistHistory>();
    public ICollection<ServiceScopeChangeRequest> ScopeChangeRequests { get; set; } = new List<ServiceScopeChangeRequest>();
    public ICollection<ServiceDisputeCase> DisputeCases { get; set; } = new List<ServiceDisputeCase>();
}
