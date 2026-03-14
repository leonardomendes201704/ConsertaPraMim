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
}
