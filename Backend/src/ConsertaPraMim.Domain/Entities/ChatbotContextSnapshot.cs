using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class ChatbotContextSnapshot : BaseEntity
{
    public Guid ConversationId { get; set; }
    public ChatbotConversation Conversation { get; set; } = null!;

    public Guid ClientId { get; set; }
    public User Client { get; set; } = null!;

    public string SnapshotType { get; set; } = string.Empty;
    public string ContextJson { get; set; } = string.Empty;
    public string? PromptVersion { get; set; }
    public string? ModelName { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
}
