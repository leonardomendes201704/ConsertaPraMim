using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ChatbotMessage : BaseEntity
{
    public Guid ConversationId { get; set; }
    public ChatbotConversation Conversation { get; set; } = null!;

    public Guid ClientId { get; set; }
    public User Client { get; set; } = null!;

    public ChatbotMessageDirection Direction { get; set; }
    public string Source { get; set; } = string.Empty;

    public string? ChannelMessageId { get; set; }
    public string? Content { get; set; }
    public string? IntentName { get; set; }
    public string? ModelName { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
    public string? MetadataJson { get; set; }
}
