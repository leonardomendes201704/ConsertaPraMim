using System.ComponentModel.DataAnnotations;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.API.Contracts;

public sealed class TelegramChatbotOpenSessionRequest
{
    [Required]
    [MaxLength(32)]
    public string Channel { get; init; } = "telegram";

    [Required]
    [MaxLength(128)]
    public string ChannelConversationId { get; init; } = string.Empty;

    [MaxLength(120)]
    public string? LastIntent { get; init; }

    [MaxLength(120)]
    public string? LastStep { get; init; }

    [MaxLength(4000)]
    public string? MetadataJson { get; init; }

    public DateTime? InteractionAtUtc { get; init; }
}

public sealed class TelegramChatbotRegisterMessageRequest
{
    [Required]
    public Guid ConversationId { get; init; }

    [Required]
    public ChatbotMessageDirection Direction { get; init; }

    [Required]
    [MaxLength(32)]
    public string Source { get; init; } = string.Empty;

    [MaxLength(128)]
    public string? ChannelMessageId { get; init; }

    [MaxLength(8000)]
    public string? Content { get; init; }

    [MaxLength(120)]
    public string? IntentName { get; init; }

    [MaxLength(120)]
    public string? ModelName { get; init; }

    [Range(0, int.MaxValue)]
    public int? PromptTokens { get; init; }

    [Range(0, int.MaxValue)]
    public int? CompletionTokens { get; init; }

    [Range(0, int.MaxValue)]
    public int? TotalTokens { get; init; }

    public DateTime? SentAtUtc { get; init; }

    [MaxLength(4000)]
    public string? MetadataJson { get; init; }

    [MaxLength(120)]
    public string? LastStep { get; init; }
}

public sealed class TelegramChatbotRegisterContextSnapshotRequest
{
    [Required]
    public Guid ConversationId { get; init; }

    [Required]
    [MaxLength(80)]
    public string SnapshotType { get; init; } = string.Empty;

    [Required]
    [MaxLength(16000)]
    public string ContextJson { get; init; } = string.Empty;

    [MaxLength(60)]
    public string? PromptVersion { get; init; }

    [MaxLength(120)]
    public string? ModelName { get; init; }

    [Range(0, int.MaxValue)]
    public int? PromptTokens { get; init; }

    [Range(0, int.MaxValue)]
    public int? CompletionTokens { get; init; }

    [Range(0, int.MaxValue)]
    public int? TotalTokens { get; init; }

    public DateTime? CapturedAtUtc { get; init; }

    [MaxLength(120)]
    public string? IntentName { get; init; }

    [MaxLength(120)]
    public string? LastStep { get; init; }
}

public sealed class TelegramChatbotRegisterActionRequest
{
    [Required]
    public Guid ConversationId { get; init; }

    [Required]
    [MaxLength(80)]
    public string ActionType { get; init; } = string.Empty;

    [Required]
    public ChatbotActionStatus Status { get; init; }

    [MaxLength(120)]
    public string? IntentName { get; init; }

    [MaxLength(16000)]
    public string? PayloadJson { get; init; }

    [MaxLength(16000)]
    public string? ResultJson { get; init; }

    [MaxLength(80)]
    public string? ErrorCode { get; init; }

    [MaxLength(1200)]
    public string? ErrorMessage { get; init; }

    [MaxLength(80)]
    public string? CorrelationId { get; init; }

    public DateTime? OccurredAtUtc { get; init; }

    [MaxLength(4000)]
    public string? MetadataJson { get; init; }

    [MaxLength(120)]
    public string? LastStep { get; init; }

    public ChatbotConversationStatus? ConversationStatus { get; init; }
}

public sealed class TelegramChatbotUpdateConversationStateRequest
{
    public ChatbotConversationStatus? Status { get; init; }

    [MaxLength(120)]
    public string? LastIntent { get; init; }

    [MaxLength(120)]
    public string? LastStep { get; init; }

    [MaxLength(4000)]
    public string? MetadataJson { get; init; }

    public DateTime? InteractionAtUtc { get; init; }
}
