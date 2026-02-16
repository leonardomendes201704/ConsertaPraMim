using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ServiceDisputeCaseMessage : BaseEntity
{
    public Guid ServiceDisputeCaseId { get; set; }
    public ServiceDisputeCase ServiceDisputeCase { get; set; } = null!;

    public Guid? AuthorUserId { get; set; }
    public User? AuthorUser { get; set; }

    public ServiceAppointmentActorRole AuthorRole { get; set; } = ServiceAppointmentActorRole.System;

    public string MessageType { get; set; } = "Comment";
    public string MessageText { get; set; } = string.Empty;
    public bool IsInternal { get; set; }

    public string? MetadataJson { get; set; }

    public ICollection<ServiceDisputeCaseAttachment> Attachments { get; set; } = new List<ServiceDisputeCaseAttachment>();
}
