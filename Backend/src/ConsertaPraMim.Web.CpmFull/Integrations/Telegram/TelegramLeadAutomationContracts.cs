namespace AppMobileCPM.Integrations.Telegram;

public sealed class TelegramLeadAutomationRequest
{
    public required string BoardType { get; init; }
    public required Guid ChatbotConversationId { get; init; }
    public required string ChannelConversationId { get; init; }
    public long TelegramChatId { get; init; }
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string UserPhone { get; init; } = string.Empty;
    public string UserEmail { get; init; } = string.Empty;
    public Guid? ServiceRequestId { get; init; }
    public string ServiceCategory { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string StatusNote { get; init; } = string.Empty;
    public string InternalNotes { get; init; } = string.Empty;
    public DateTime? LastContactAtUtc { get; init; }
}

public sealed class TelegramLeadAutomationResponse
{
    public bool Success { get; init; }
    public int LeadId { get; init; }
    public bool Created { get; init; }
    public string BoardType { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string ChatwootStatus { get; init; } = string.Empty;
    public string ChatwootMessage { get; init; } = string.Empty;
    public long? ChatwootContactId { get; init; }
    public long? ChatwootConversationId { get; init; }
    public long? ChatwootInboxId { get; init; }
}

public sealed class TelegramLeadAutomationResult
{
    public int HttpStatusCode { get; init; }
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public TelegramLeadAutomationResponse? Payload { get; init; }

    public static TelegramLeadAutomationResult Ok(TelegramLeadAutomationResponse payload) =>
        new()
        {
            HttpStatusCode = StatusCodes.Status200OK,
            Success = true,
            Message = payload.Message,
            Payload = payload
        };

    public static TelegramLeadAutomationResult Fail(int httpStatusCode, string message) =>
        new()
        {
            HttpStatusCode = httpStatusCode,
            Success = false,
            Message = message
        };
}
