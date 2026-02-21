namespace ConsertaPraMim.Application.DTOs;

public record SupportTicketAttachmentInputDto(
    string FileUrl,
    string FileName,
    string ContentType,
    long SizeBytes);

public record SupportTicketAttachmentDto(
    Guid Id,
    string FileUrl,
    string FileName,
    string ContentType,
    long SizeBytes,
    string MediaKind);

public record SupportTicketUploadAttachmentDto(
    string FileUrl,
    string FileName,
    string ContentType,
    long SizeBytes,
    string MediaKind);
