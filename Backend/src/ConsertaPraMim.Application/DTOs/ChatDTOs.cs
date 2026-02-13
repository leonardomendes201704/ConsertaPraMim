namespace ConsertaPraMim.Application.DTOs;

public record ChatAttachmentInputDto(
    string FileUrl,
    string FileName,
    string ContentType,
    long SizeBytes);

public record ChatAttachmentDto(
    Guid Id,
    string FileUrl,
    string FileName,
    string ContentType,
    long SizeBytes,
    string MediaKind);

public record ChatMessageDto(
    Guid Id,
    Guid RequestId,
    Guid ProviderId,
    Guid SenderId,
    string SenderName,
    string SenderRole,
    string? Text,
    DateTime CreatedAt,
    IReadOnlyList<ChatAttachmentDto> Attachments);
