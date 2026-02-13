using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ChatMessage : BaseEntity
{
    public Guid RequestId { get; set; }
    public ServiceRequest Request { get; set; } = null!;

    public Guid ProviderId { get; set; }

    public Guid SenderId { get; set; }
    public User Sender { get; set; } = null!;

    public UserRole SenderRole { get; set; }
    public string? Text { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }

    public ICollection<ChatAttachment> Attachments { get; set; } = new List<ChatAttachment>();
}
