using Microsoft.AspNetCore.Http;

namespace ConsertaPraMim.Web.TelegramBridge.Models;

public sealed class TelegramJourneySchedulingTurnRequest
{
    public required Guid ChatbotConversationId { get; init; }
    public string ChannelConversationId { get; init; } = string.Empty;
    public long TelegramChatId { get; init; }
    public string MessageText { get; init; } = string.Empty;
    public DateTime? MessageSentAtUtc { get; init; }
}

public sealed class TelegramJourneySchedulingTurnResult
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
    public IReadOnlyList<TelegramJourneySchedulingSuggestedSlot> SuggestedSlots { get; init; } = [];

    public static TelegramJourneySchedulingTurnResult NoOp() => new()
    {
        Success = true,
        Handled = false,
        HttpStatusCode = StatusCodes.Status200OK,
        Message = "Nenhuma acao de agendamento aplicada para esta mensagem."
    };

    public static TelegramJourneySchedulingTurnResult Disabled(string message) => new()
    {
        Success = false,
        Handled = false,
        HttpStatusCode = StatusCodes.Status409Conflict,
        Message = message
    };

    public static TelegramJourneySchedulingTurnResult Failed(int httpStatusCode, string message) => new()
    {
        Success = false,
        Handled = false,
        HttpStatusCode = httpStatusCode,
        Message = message
    };
}

public sealed class TelegramJourneySchedulingSuggestedSlot
{
    public int OptionNumber { get; init; }
    public DateTime StartsAtUtc { get; init; }
    public DateTime EndsAtUtc { get; init; }
    public string Label { get; init; } = string.Empty;
}
