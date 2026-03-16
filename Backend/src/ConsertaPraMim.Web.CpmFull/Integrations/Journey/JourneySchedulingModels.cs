using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneySchedulingTurnRequest
{
    public required Guid ChatbotConversationId { get; init; }
    public string ChannelConversationId { get; init; } = string.Empty;
    public long TelegramChatId { get; init; }
    public string MessageText { get; init; } = string.Empty;
    public DateTime? MessageSentAtUtc { get; init; }
}

public sealed class JourneySchedulingTurnResult
{
    public bool Success { get; init; }
    public bool Handled { get; init; }
    public int HttpStatusCode { get; init; }
    public int LeadId { get; init; }
    public int JourneyId { get; init; }
    public string CurrentState { get; init; } = string.Empty;
    public string SchedulingStatus { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string ReplyText { get; init; } = string.Empty;
    public bool RemoveReplyKeyboard { get; init; }
    public string GoogleCalendarEventId { get; init; } = string.Empty;
    public string GoogleCalendarEventLink { get; init; } = string.Empty;
    public DateTime? ScheduledStartAtUtc { get; init; }
    public DateTime? ScheduledEndAtUtc { get; init; }
    public IReadOnlyList<JourneySchedulingSuggestedSlot> SuggestedSlots { get; init; } = [];

    public static JourneySchedulingTurnResult NoOp() => new()
    {
        Success = true,
        Handled = false,
        HttpStatusCode = StatusCodes.Status200OK,
        Message = "Nenhuma acao de agendamento aplicada para esta mensagem."
    };

    public static JourneySchedulingTurnResult Disabled(string message) => new()
    {
        Success = false,
        Handled = false,
        HttpStatusCode = StatusCodes.Status409Conflict,
        Message = message
    };

    public static JourneySchedulingTurnResult Failed(int httpStatusCode, string message) => new()
    {
        Success = false,
        Handled = false,
        HttpStatusCode = httpStatusCode,
        Message = message
    };
}

public sealed class JourneySchedulingSuggestedSlot
{
    public int OptionNumber { get; init; }
    public DateTime StartsAtUtc { get; init; }
    public DateTime EndsAtUtc { get; init; }
    public string Label { get; init; } = string.Empty;
}

public sealed class JourneyCalendarBusySlot
{
    public DateTime StartsAtUtc { get; init; }
    public DateTime EndsAtUtc { get; init; }
}

public sealed class JourneyCalendarEventUpsertRequest
{
    public required string Title { get; init; }
    public DateTime StartsAtUtc { get; init; }
    public DateTime EndsAtUtc { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
    public string IdempotencyKey { get; init; } = string.Empty;
}

public sealed class JourneyCalendarEventUpsertResult
{
    public bool Success { get; init; }
    public string EventId { get; init; } = string.Empty;
    public string EventLink { get; init; } = string.Empty;
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed class JourneyCalendarEventDeleteResult
{
    public bool Success { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}

internal sealed class JourneyGoogleAccessTokenEnvelope
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }
}
