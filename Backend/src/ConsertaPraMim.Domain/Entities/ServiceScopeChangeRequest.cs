using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ServiceScopeChangeRequest : BaseEntity
{
    public Guid ServiceRequestId { get; set; }
    public ServiceRequest ServiceRequest { get; set; } = null!;

    public Guid ServiceAppointmentId { get; set; }
    public ServiceAppointment ServiceAppointment { get; set; } = null!;

    public Guid ProviderId { get; set; }
    public User Provider { get; set; } = null!;

    public int Version { get; set; } = 1;
    public ServiceScopeChangeRequestStatus Status { get; set; } = ServiceScopeChangeRequestStatus.PendingClientApproval;

    public string Reason { get; set; } = string.Empty;
    public string AdditionalScopeDescription { get; set; } = string.Empty;
    public decimal IncrementalValue { get; set; }

    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClientRespondedAtUtc { get; set; }
    public string? ClientResponseReason { get; set; }

    public Guid? PreviousVersionId { get; set; }
    public ServiceScopeChangeRequest? PreviousVersion { get; set; }
    public ICollection<ServiceScopeChangeRequest> NextVersions { get; set; } = new List<ServiceScopeChangeRequest>();
}
