using System.Text.Json.Serialization;

namespace ConsertaPraMim.Web.TelegramBridge.Models;

public sealed record ChatConversationSummaryDto(
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] long ChatId,
    string Title,
    string LastMessagePreview,
    DateTimeOffset UpdatedAtUtc,
    int TotalMessages);
