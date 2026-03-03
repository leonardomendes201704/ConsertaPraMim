using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ChatbotConversation : BaseEntity
{
    public Guid ClientId { get; set; }
    public User Client { get; set; } = null!;

    public string Channel { get; set; } = "telegram";
    public string ChannelConversationId { get; set; } = string.Empty;

    public ChatbotConversationStatus Status { get; set; } = ChatbotConversationStatus.Active;

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastInteractionAtUtc { get; set; } = DateTime.UtcNow;

    public string? LastIntent { get; set; }
    public string? LastStep { get; set; }
    public string? MetadataJson { get; set; }

    public ICollection<ChatbotMessage> Messages { get; set; } = new List<ChatbotMessage>();
    public ICollection<ChatbotContextSnapshot> ContextSnapshots { get; set; } = new List<ChatbotContextSnapshot>();
    public ICollection<ChatbotActionLog> ActionLogs { get; set; } = new List<ChatbotActionLog>();
}
