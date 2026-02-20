using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class SupportTicketMessage : BaseEntity
{
    public Guid SupportTicketId { get; set; }
    public SupportTicket SupportTicket { get; set; } = null!;

    public Guid? AuthorUserId { get; set; }
    public User? AuthorUser { get; set; }

    public UserRole AuthorRole { get; set; } = UserRole.Provider;
    public string MessageType { get; set; } = "Comment";
    public string MessageText { get; set; } = string.Empty;
    public bool IsInternal { get; set; }

    public string? MetadataJson { get; set; }
}
