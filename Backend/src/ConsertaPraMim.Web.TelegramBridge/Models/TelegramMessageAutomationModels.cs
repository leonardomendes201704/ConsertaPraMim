namespace ConsertaPraMim.Web.TelegramBridge.Models;

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

public sealed class TelegramInboundMessageAutomationResult
{
    public bool Success { get; init; }
    public int HttpStatusCode { get; init; }
    public int LeadId { get; init; }
    public string QueueStatus { get; init; } = string.Empty;
    public bool Duplicate { get; init; }
    public string Message { get; init; } = string.Empty;

    public static TelegramInboundMessageAutomationResult Disabled(string message) =>
        new()
        {
            Success = false,
            HttpStatusCode = StatusCodes.Status409Conflict,
            Message = message
        };

    public static TelegramInboundMessageAutomationResult Failed(int httpStatusCode, string message) =>
        new()
        {
            Success = false,
            HttpStatusCode = httpStatusCode,
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
    public long TelegramChatId { get; init; }
    public bool IsActive { get; init; }
    public string HandoffStatus { get; init; } = string.Empty;
    public string ReasonCode { get; init; } = string.Empty;
    public string ReasonLabel { get; init; } = string.Empty;
    public DateTime? StartedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class TelegramBridgeResetHandoffRequest
{
    public long TelegramChatId { get; init; }
}

public sealed class TelegramBridgeResetHandoffResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public long TelegramChatId { get; init; }
    public bool HandoffWasActive { get; init; }
}
