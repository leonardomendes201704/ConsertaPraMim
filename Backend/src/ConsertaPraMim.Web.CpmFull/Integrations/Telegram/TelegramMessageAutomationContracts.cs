using System.Text.Json.Serialization;

namespace AppMobileCPM.Integrations.Telegram;

public static class TelegramDeliveryDirections
{
    public const string TelegramToChatwoot = "telegram_to_chatwoot";
    public const string ChatwootToTelegram = "chatwoot_to_telegram";
}

public static class TelegramDeliveryQueueStatuses
{
    public const string Queued = "queued";
    public const string Processing = "processing";
    public const string Retrying = "retrying";
    public const string Processed = "processed";
    public const string DeadLetter = "dead_letter";
}

public sealed class TelegramInboundMessageAutomationRequest
{
    public Guid? ChatbotConversationId { get; init; }
    public required string ChannelConversationId { get; init; }
    public required string ChannelMessageId { get; init; }
    public long TelegramChatId { get; init; }
    public string SenderDisplayName { get; init; } = string.Empty;
    public string MessageText { get; init; } = string.Empty;
    public DateTime SentAtUtc { get; init; }
    public IReadOnlyList<TelegramInboundAttachmentDto> Attachments { get; init; } = [];
}

public sealed class TelegramInboundAttachmentDto
{
    public string FileName { get; init; } = string.Empty;
    public string MediaKind { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
}

public sealed class TelegramInboundMessageAutomationResponse
{
    public bool Success { get; init; }
    public int LeadId { get; init; }
    public string QueueStatus { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public bool Duplicate { get; init; }
}

public sealed class TelegramInboundMessageAutomationResult
{
    public int HttpStatusCode { get; init; }
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public TelegramInboundMessageAutomationResponse? Payload { get; init; }

    public static TelegramInboundMessageAutomationResult Ok(int httpStatusCode, TelegramInboundMessageAutomationResponse payload) =>
        new()
        {
            HttpStatusCode = httpStatusCode,
            Success = true,
            Message = payload.Message,
            Payload = payload
        };

    public static TelegramInboundMessageAutomationResult Fail(int httpStatusCode, string message) =>
        new()
        {
            HttpStatusCode = httpStatusCode,
            Success = false,
            Message = message
        };
}

public sealed class TelegramToChatwootDeliveryPayload
{
    public int LeadId { get; init; }
    public Guid? ChatbotConversationId { get; init; }
    public required string ChannelConversationId { get; init; }
    public required string ChannelMessageId { get; init; }
    public long TelegramChatId { get; init; }
    public string SenderDisplayName { get; init; } = string.Empty;
    public string MessageText { get; init; } = string.Empty;
    public DateTime SentAtUtc { get; init; }
    public IReadOnlyList<TelegramInboundAttachmentDto> Attachments { get; init; } = [];
}

public sealed class ChatwootToTelegramDeliveryPayload
{
    public int LeadId { get; init; }
    public long ChatwootConversationId { get; init; }
    public long? ChatwootMessageId { get; init; }
    public long TelegramChatId { get; init; }
    public string SenderName { get; init; } = string.Empty;
    public string MessageText { get; init; } = string.Empty;
    public DateTime OccurredAtUtc { get; init; }
    public bool ActivateHumanHandoff { get; init; }
}

public sealed class TelegramDeliveryProcessResult
{
    public bool Succeeded { get; init; }
    public bool RetrySuggested { get; init; }
    public string Message { get; init; } = string.Empty;

    public static TelegramDeliveryProcessResult Ok(string message) =>
        new()
        {
            Succeeded = true,
            Message = message
        };

    public static TelegramDeliveryProcessResult Failed(string message, bool retrySuggested) =>
        new()
        {
            Succeeded = false,
            RetrySuggested = retrySuggested,
            Message = message
        };
}

public sealed class TelegramBridgeHumanReplyRequest
{
    public int LeadId { get; init; }
    public long TelegramChatId { get; init; }
    public long? ChatwootConversationId { get; init; }
    public long? ChatwootMessageId { get; init; }
    public string SenderName { get; init; } = string.Empty;
    public string MessageText { get; init; } = string.Empty;
    public bool ActivateHumanHandoff { get; init; }
}

public sealed class TelegramBridgeHumanReplyResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
    public long TelegramChatId { get; init; }
    public bool HumanHandoffActivated { get; init; }
}

public sealed class TelegramBridgeHumanReplyResult
{
    public bool Success { get; init; }
    public int HttpStatusCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool HumanHandoffActivated { get; init; }

    public static TelegramBridgeHumanReplyResult Failed(int httpStatusCode, string message) =>
        new()
        {
            Success = false,
            HttpStatusCode = httpStatusCode,
            Message = message
        };
}
