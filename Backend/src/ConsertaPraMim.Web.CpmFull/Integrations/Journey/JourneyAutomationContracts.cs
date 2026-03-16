namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyAutomationRequest
{
    public required string BoardType { get; init; }
    public required string SourceChannel { get; init; }
    public string SourceOrigin { get; init; } = string.Empty;
    public required string Name { get; init; }
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
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
    public Guid? LandingLeadId { get; init; }
    public Guid? ServiceRequestId { get; init; }
    public Guid? ClientId { get; init; }
    public string VisitorId { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public Guid? ChatbotConversationId { get; init; }
    public string ChannelConversationId { get; init; } = string.Empty;
    public long? TelegramChatId { get; init; }
    public DateTime? RequestedAtUtc { get; init; }
    public DateTime? LastContactAtUtc { get; init; }
}

public sealed class JourneyAutomationResponse
{
    public bool Success { get; init; }
    public int LeadId { get; init; }
    public int JourneyId { get; init; }
    public Guid JourneyPublicId { get; init; }
    public bool CreatedLead { get; init; }
    public bool CreatedJourney { get; init; }
    public string BoardType { get; init; } = string.Empty;
    public string CurrentState { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string ChatwootStatus { get; init; } = string.Empty;
    public string ChatwootMessage { get; init; } = string.Empty;
    public long? ChatwootContactId { get; init; }
    public long? ChatwootConversationId { get; init; }
    public long? ChatwootInboxId { get; init; }
}

public sealed class JourneyAutomationResult
{
    public int HttpStatusCode { get; init; }
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public JourneyAutomationResponse? Payload { get; init; }

    public static JourneyAutomationResult Ok(JourneyAutomationResponse payload) =>
        new()
        {
            HttpStatusCode = StatusCodes.Status200OK,
            Success = true,
            Message = payload.Message,
            Payload = payload
        };

    public static JourneyAutomationResult Fail(int httpStatusCode, string message) =>
        new()
        {
            HttpStatusCode = httpStatusCode,
            Success = false,
            Message = message
        };
}
