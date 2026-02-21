namespace ConsertaPraMim.Application.DTOs;

public record MobileProviderCreateSupportTicketRequestDto(
    string Subject,
    string? Category,
    int? Priority,
    string InitialMessage);

public record MobileProviderSupportTicketMessageRequestDto(
    string Message,
    IReadOnlyList<SupportTicketAttachmentInputDto>? Attachments = null);

public record MobileProviderSupportTicketListQueryDto(
    string? Status = null,
    string? Priority = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);

public record MobileProviderSupportTicketMessageDto(
    Guid Id,
    Guid? AuthorUserId,
    string AuthorRole,
    string AuthorName,
    string MessageType,
    string MessageText,
    IReadOnlyList<SupportTicketAttachmentDto> Attachments,
    DateTime CreatedAtUtc);

public record MobileProviderSupportTicketSummaryDto(
    Guid Id,
    string Subject,
    string Category,
    string Priority,
    string Status,
    DateTime OpenedAtUtc,
    DateTime LastInteractionAtUtc,
    DateTime? ClosedAtUtc,
    Guid? AssignedAdminUserId,
    string? AssignedAdminName,
    int MessageCount,
    string? LastMessagePreview);

public record MobileProviderSupportTicketDetailsDto(
    MobileProviderSupportTicketSummaryDto Ticket,
    DateTime? FirstAdminResponseAtUtc,
    IReadOnlyList<MobileProviderSupportTicketMessageDto> Messages);

public record MobileProviderSupportTicketListResponseDto(
    IReadOnlyList<MobileProviderSupportTicketSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public record MobileProviderSupportTicketOperationResultDto(
    bool Success,
    MobileProviderSupportTicketDetailsDto? Ticket = null,
    MobileProviderSupportTicketMessageDto? Message = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);
