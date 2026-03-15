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
    public string HandoffReasonCode { get; init; } = string.Empty;
    public string HandoffReasonLabel { get; init; } = string.Empty;
    public string HandoffSource { get; init; } = string.Empty;
    public DateTime? HandoffActivatedAtUtc { get; init; }
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

public sealed class TelegramBridgeSetHandoffRequest
{
    public long TelegramChatId { get; init; }
    public string ReasonCode { get; init; } = string.Empty;
    public string ReasonLabel { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public DateTime? OccurredAtUtc { get; init; }
}

public sealed class TelegramBridgeSetHandoffResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
    public long TelegramChatId { get; init; }
    public bool IsActive { get; init; }
    public string HandoffStatus { get; init; } = string.Empty;
    public string ReasonCode { get; init; } = string.Empty;
    public string ReasonLabel { get; init; } = string.Empty;
    public DateTime? StartedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class TelegramBridgeSetHandoffResult
{
    public bool Success { get; init; }
    public int HttpStatusCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public string HandoffStatus { get; init; } = string.Empty;
    public string ReasonCode { get; init; } = string.Empty;
    public string ReasonLabel { get; init; } = string.Empty;
    public DateTime? StartedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }

    public static TelegramBridgeSetHandoffResult Failed(int httpStatusCode, string message) =>
        new()
        {
            Success = false,
            HttpStatusCode = httpStatusCode,
            Message = message
        };
}

public sealed class TelegramBridgeResetHandoffRequest
{
    public long TelegramChatId { get; init; }
}

public sealed class TelegramBridgeResetHandoffResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
    public long TelegramChatId { get; init; }
    public bool HandoffWasActive { get; init; }
}

public sealed class TelegramBridgeResetHandoffResult
{
    public bool Success { get; init; }
    public int HttpStatusCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool HandoffWasActive { get; init; }

    public static TelegramBridgeResetHandoffResult Failed(int httpStatusCode, string message) =>
        new()
        {
            Success = false,
            HttpStatusCode = httpStatusCode,
            Message = message
        };
}
