namespace ConsertaPraMim.Web.TelegramBridge.Models;

public sealed record ChatConversationSummaryDto(
    long ChatId,
    string Title,
    string LastMessagePreview,
    DateTimeOffset UpdatedAtUtc,
    int TotalMessages);
