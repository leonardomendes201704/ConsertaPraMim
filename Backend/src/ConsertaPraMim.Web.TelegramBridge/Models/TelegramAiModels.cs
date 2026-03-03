namespace ConsertaPraMim.Web.TelegramBridge.Models;

public sealed record TelegramAiPromptMessage(string Role, string Content);

public sealed record TelegramAiGatewayRequest(
    string ApiKey,
    string Model,
    decimal Temperature,
    int MaxOutputTokens,
    IReadOnlyList<TelegramAiPromptMessage> Messages,
    int RequestTimeoutSeconds,
    int MaxRetries);

public sealed record TelegramAiGatewayResult(
    bool Success,
    string? OutputText = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    int? InputTokens = null,
    int? OutputTokens = null,
    int? TotalTokens = null,
    int AttemptCount = 0,
    long LatencyMilliseconds = 0);

public sealed record TelegramAiStructuredResponse(
    string MessageToClient,
    string Intent,
    string NextStep,
    decimal? Confidence,
    string? EntitiesJson);

public sealed record TelegramChatbotAssistantReply(
    string MessageText,
    string Intent,
    string NextStep,
    decimal? Confidence,
    string? EntitiesJson,
    bool UsedFallback,
    bool UsedCache,
    int? PromptTokens,
    int? CompletionTokens,
    int? TotalTokens);

public sealed record TelegramChatbotConversationHistoryDto(
    TelegramChatbotConversationDto Conversation,
    IReadOnlyList<TelegramChatbotMessageDto> Messages,
    IReadOnlyList<TelegramChatbotContextSnapshotDto> ContextSnapshots,
    IReadOnlyList<TelegramChatbotActionLogDto> ActionLogs);

public sealed record TelegramChatbotConversationDto(
    Guid Id,
    Guid ClientId,
    string Channel,
    string ChannelConversationId,
    int Status,
    DateTime StartedAtUtc,
    DateTime LastInteractionAtUtc,
    string? LastIntent,
    string? LastStep,
    string? MetadataJson);

public sealed record TelegramChatbotMessageDto(
    Guid Id,
    Guid ConversationId,
    Guid ClientId,
    int Direction,
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

public sealed record TelegramChatbotContextSnapshotDto(
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

public sealed record TelegramChatbotActionLogDto(
    Guid Id,
    Guid ConversationId,
    Guid ClientId,
    string ActionType,
    int Status,
    string? IntentName,
    string? PayloadJson,
    string? ResultJson,
    string? ErrorCode,
    string? ErrorMessage,
    string? CorrelationId,
    DateTime OccurredAtUtc,
    string? MetadataJson);
