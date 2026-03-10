namespace ConsertaPraMim.Web.TelegramBridge.Models;

public sealed record ChatAttachmentDto(
    string Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Url,
    string MediaKind);
