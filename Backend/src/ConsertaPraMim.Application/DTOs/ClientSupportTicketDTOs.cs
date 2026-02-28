namespace ConsertaPraMim.Application.DTOs;

public record ClientSupportTicketMessageRequestDto(
    string? Message,
    IReadOnlyList<SupportTicketAttachmentInputDto>? Attachments = null);

public record ClientSupportTicketMessageDto(
    Guid Id,
    Guid? AuthorUserId,
    string AuthorRole,
    string AuthorName,
    string MessageType,
    string MessageText,
    IReadOnlyList<SupportTicketAttachmentDto> Attachments,
    DateTime CreatedAtUtc);

public record ClientSupportTicketSummaryDto(
    Guid Id,
    Guid ServiceRequestId,
    string Subject,
    string Category,
    string Priority,
    string Status,
    DateTime OpenedAtUtc,
    DateTime LastInteractionAtUtc,
    DateTime? FirstAdminResponseAtUtc,
    DateTime? ClosedAtUtc,
    Guid? AssignedAdminUserId,
    string? AssignedAdminName,
    int MessageCount);

public record ClientSupportTicketDetailsDto(
    ClientSupportTicketSummaryDto Ticket,
    IReadOnlyList<ClientSupportTicketMessageDto> Messages);

public record ClientSupportTicketOperationResultDto(
    bool Success,
    ClientSupportTicketDetailsDto? Ticket = null,
    ClientSupportTicketMessageDto? Message = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);
