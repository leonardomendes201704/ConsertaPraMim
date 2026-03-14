namespace ConsertaPraMim.Web.TelegramBridge.Models;

public sealed class TelegramLeadAutomationUpsertRequest
{
    public required string BoardType { get; init; }
    public required Guid ChatbotConversationId { get; init; }
    public required string ChannelConversationId { get; init; }
    public long TelegramChatId { get; init; }
    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public string ClientEmail { get; init; } = string.Empty;
    public Guid? ServiceRequestId { get; init; }
    public string ServiceCategory { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string StatusNote { get; init; } = string.Empty;
    public string InternalNotes { get; init; } = string.Empty;
    public DateTime? LastContactAtUtc { get; init; }
}

public sealed class TelegramLeadAutomationUpsertResult
{
    public bool Success { get; init; }
    public int HttpStatusCode { get; init; }
    public int LeadId { get; init; }
    public bool Created { get; init; }
    public string BoardType { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string ChatwootStatus { get; init; } = string.Empty;
    public string ChatwootMessage { get; init; } = string.Empty;
    public long? ChatwootContactId { get; init; }
    public long? ChatwootConversationId { get; init; }
    public long? ChatwootInboxId { get; init; }

    public static TelegramLeadAutomationUpsertResult Disabled(string message) =>
        new()
        {
            Success = false,
            HttpStatusCode = StatusCodes.Status409Conflict,
            Message = message
        };

    public static TelegramLeadAutomationUpsertResult Failed(int httpStatusCode, string message) =>
        new()
        {
            Success = false,
            HttpStatusCode = httpStatusCode,
            Message = message
        };
}
