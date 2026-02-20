using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class SupportTicket : BaseEntity
{
    public Guid ProviderId { get; set; }
    public User Provider { get; set; } = null!;

    public Guid? AssignedAdminUserId { get; set; }
    public User? AssignedAdminUser { get; set; }

    public string Subject { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public SupportTicketPriority Priority { get; set; } = SupportTicketPriority.Medium;
    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.Open;

    public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastInteractionAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AssignedAtUtc { get; set; }
    public DateTime? FirstAdminResponseAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }

    public string? MetadataJson { get; set; }

    public ICollection<SupportTicketMessage> Messages { get; set; } = new List<SupportTicketMessage>();

    public SupportTicketMessage AddMessage(
        Guid? authorUserId,
        UserRole authorRole,
        string messageText,
        bool isInternal = false,
        string messageType = "Comment",
        string? metadataJson = null)
    {
        var normalizedText = (messageText ?? string.Empty).Trim();
        if (normalizedText.Length == 0)
        {
            throw new ArgumentException("Message text cannot be empty.", nameof(messageText));
        }

        var normalizedMessageType = string.IsNullOrWhiteSpace(messageType)
            ? "Comment"
            : messageType.Trim();

        var now = DateTime.UtcNow;
        var message = new SupportTicketMessage
        {
            SupportTicketId = Id,
            AuthorUserId = authorUserId,
            AuthorRole = authorRole,
            MessageType = normalizedMessageType,
            MessageText = normalizedText,
            IsInternal = isInternal,
            MetadataJson = metadataJson
        };

        Messages.Add(message);
        LastInteractionAtUtc = now;
        UpdatedAt = now;

        if (authorRole == UserRole.Admin && !FirstAdminResponseAtUtc.HasValue)
        {
            FirstAdminResponseAtUtc = now;
        }

        return message;
    }

    public void AssignAdmin(Guid adminUserId)
    {
        if (adminUserId == Guid.Empty)
        {
            throw new ArgumentException("Admin user id cannot be empty.", nameof(adminUserId));
        }

        var now = DateTime.UtcNow;
        AssignedAdminUserId = adminUserId;
        AssignedAtUtc = now;
        UpdatedAt = now;
    }

    public void ChangeStatus(SupportTicketStatus status)
    {
        var now = DateTime.UtcNow;
        Status = status;
        UpdatedAt = now;

        if (status is SupportTicketStatus.Resolved or SupportTicketStatus.Closed)
        {
            ClosedAtUtc ??= now;
        }
        else
        {
            ClosedAtUtc = null;
        }
    }
}
