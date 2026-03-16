namespace ConsertaPraMim.Application.DTOs;

public sealed class ServiceJourneyAutomationRequestDto
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

public sealed class ServiceJourneyAutomationResultDto
{
    public bool Success { get; init; }
    public int HttpStatusCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public int? LeadId { get; init; }
    public int? JourneyId { get; init; }
    public Guid? JourneyPublicId { get; init; }
    public bool CreatedLead { get; init; }
    public bool CreatedJourney { get; init; }
    public string BoardType { get; init; } = string.Empty;
    public string CurrentState { get; init; } = string.Empty;

    public static ServiceJourneyAutomationResultDto Disabled(string message) =>
        new() { Success = false, HttpStatusCode = 409, Message = message };

    public static ServiceJourneyAutomationResultDto Failed(int httpStatusCode, string message) =>
        new() { Success = false, HttpStatusCode = httpStatusCode, Message = message };
}
