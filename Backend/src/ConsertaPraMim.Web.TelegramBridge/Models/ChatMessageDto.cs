namespace ConsertaPraMim.Web.TelegramBridge.Models;

public sealed record ChatMessageDto(
    string Id,
    long ChatId,
    bool IsOutgoing,
    string SenderDisplayName,
    string? Text,
    DateTimeOffset SentAtUtc,
    IReadOnlyList<ChatAttachmentDto> Attachments);
