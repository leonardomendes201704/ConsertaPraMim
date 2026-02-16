using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ServiceDisputeCaseAuditEntry : BaseEntity
{
    public Guid ServiceDisputeCaseId { get; set; }
    public ServiceDisputeCase ServiceDisputeCase { get; set; } = null!;

    public Guid? ActorUserId { get; set; }
    public User? ActorUser { get; set; }

    public ServiceAppointmentActorRole ActorRole { get; set; } = ServiceAppointmentActorRole.System;

    public string EventType { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? MetadataJson { get; set; }
}
