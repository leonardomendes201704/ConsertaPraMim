using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ChatbotActionLog : BaseEntity
{
    public Guid ConversationId { get; set; }
    public ChatbotConversation Conversation { get; set; } = null!;

    public Guid ClientId { get; set; }
    public User Client { get; set; } = null!;

    public string ActionType { get; set; } = string.Empty;
    public ChatbotActionStatus Status { get; set; } = ChatbotActionStatus.Pending;
    public string? IntentName { get; set; }
    public string? PayloadJson { get; set; }
    public string? ResultJson { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string? MetadataJson { get; set; }
}
