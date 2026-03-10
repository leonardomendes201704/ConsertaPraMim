using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public record TelegramChatbotConversationDto(
    Guid Id,
    Guid ClientId,
    string Channel,
    string ChannelConversationId,
    ChatbotConversationStatus Status,
    DateTime StartedAtUtc,
    DateTime LastInteractionAtUtc,
    string? LastIntent,
    string? LastStep,
    string? MetadataJson);

public record TelegramChatbotMessageDto(
    Guid Id,
    Guid ConversationId,
    Guid ClientId,
    ChatbotMessageDirection Direction,
    string Source,
    string? ChannelMessageId,
    string? Content,
    string? IntentName,
    string? ModelName,
    int? PromptTokens,
    int? CompletionTokens,
    int? TotalTokens,
    DateTime SentAtUtc,
    string? MetadataJson);

public record TelegramChatbotContextSnapshotDto(
    Guid Id,
    Guid ConversationId,
    Guid ClientId,
    string SnapshotType,
    string ContextJson,
    string? PromptVersion,
    string? ModelName,
    int? PromptTokens,
    int? CompletionTokens,
    int? TotalTokens,
    DateTime CapturedAtUtc);

public record TelegramChatbotActionLogDto(
    Guid Id,
    Guid ConversationId,
    Guid ClientId,
    string ActionType,
    ChatbotActionStatus Status,
    string? IntentName,
    string? PayloadJson,
    string? ResultJson,
    string? ErrorCode,
    string? ErrorMessage,
    string? CorrelationId,
    DateTime OccurredAtUtc,
    string? MetadataJson);

public record TelegramChatbotConversationHistoryDto(
    TelegramChatbotConversationDto Conversation,
    IReadOnlyList<TelegramChatbotMessageDto> Messages,
    IReadOnlyList<TelegramChatbotContextSnapshotDto> ContextSnapshots,
    IReadOnlyList<TelegramChatbotActionLogDto> ActionLogs);

public record TelegramChatbotOpenConversationRequestDto(
    Guid ClientId,
    string Channel,
    string ChannelConversationId,
    ChatbotConversationStatus Status = ChatbotConversationStatus.Active,
    string? LastIntent = null,
    string? LastStep = null,
    string? MetadataJson = null,
    DateTime? InteractionAtUtc = null);

public record TelegramChatbotUpdateConversationStateRequestDto(
    Guid ConversationId,
    Guid ClientId,
    ChatbotConversationStatus? Status = null,
    string? LastIntent = null,
    string? LastStep = null,
    string? MetadataJson = null,
    DateTime? InteractionAtUtc = null);

public record TelegramChatbotRegisterMessageRequestDto(
    Guid ConversationId,
    Guid ClientId,
    ChatbotMessageDirection Direction,
    string Source,
    string? ChannelMessageId = null,
    string? Content = null,
    string? IntentName = null,
    string? ModelName = null,
    int? PromptTokens = null,
    int? CompletionTokens = null,
    int? TotalTokens = null,
    DateTime? SentAtUtc = null,
    string? MetadataJson = null,
    string? LastStep = null);

public record TelegramChatbotRegisterContextSnapshotRequestDto(
    Guid ConversationId,
    Guid ClientId,
    string SnapshotType,
    string ContextJson,
    string? PromptVersion = null,
    string? ModelName = null,
    int? PromptTokens = null,
    int? CompletionTokens = null,
    int? TotalTokens = null,
    DateTime? CapturedAtUtc = null,
    string? IntentName = null,
    string? LastStep = null);

public record TelegramChatbotRegisterActionLogRequestDto(
    Guid ConversationId,
    Guid ClientId,
    string ActionType,
    ChatbotActionStatus Status,
    string? IntentName = null,
    string? PayloadJson = null,
    string? ResultJson = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    string? CorrelationId = null,
    DateTime? OccurredAtUtc = null,
    string? MetadataJson = null,
    string? LastStep = null,
    ChatbotConversationStatus? ConversationStatus = null);
