using System.Text.Json.Serialization;

namespace ConsertaPraMim.Web.TelegramBridge.Models;

public sealed record ChatMessageDto(
    string Id,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] long ChatId,
    bool IsOutgoing,
    string SenderDisplayName,
    string? Text,
    DateTimeOffset SentAtUtc,
    IReadOnlyList<ChatAttachmentDto> Attachments);
