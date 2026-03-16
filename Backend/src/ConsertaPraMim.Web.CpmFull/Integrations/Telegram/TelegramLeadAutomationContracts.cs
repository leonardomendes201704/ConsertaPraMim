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
    public string ProblemDescription { get; init; } = string.Empty;
    public string Street { get; init; } = string.Empty;
    public string Neighborhood { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
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
    public bool HasPhone { get; init; }
    public bool HasEmail { get; init; }
    public bool HasCity { get; init; }
    public bool HasServiceCategory { get; init; }
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
