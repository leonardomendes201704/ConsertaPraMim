namespace ConsertaPraMim.Application.DTOs;

public record AdminSupportTicketListQueryDto(
    string? Status = null,
    string? Priority = null,
    Guid? AssignedAdminUserId = null,
    bool? AssignedOnly = null,
    string? Search = null,
    string? SortBy = null,
    bool SortDescending = true,
    int Page = 1,
    int PageSize = 20,
    int FirstResponseSlaMinutes = 60);

public record AdminSupportTicketQueueIndicatorsDto(
    int OpenCount,
    int InProgressCount,
    int WaitingProviderCount,
    int ResolvedCount,
    int ClosedCount,
    int WithoutFirstAdminResponseCount,
    int OverdueWithoutFirstResponseCount,
    int UnassignedCount);

public record AdminSupportTicketSummaryDto(
    Guid Id,
    Guid ProviderId,
    string ProviderName,
    string ProviderEmail,
    Guid? AssignedAdminUserId,
    string? AssignedAdminName,
    string Subject,
    string Category,
    string Priority,
    string Status,
    DateTime OpenedAtUtc,
    DateTime LastInteractionAtUtc,
    DateTime? FirstAdminResponseAtUtc,
    DateTime? ClosedAtUtc,
    int MessageCount,
    string? LastMessagePreview,
    bool IsOverdueFirstResponse);

public record AdminSupportTicketMessageDto(
    Guid Id,
    Guid? AuthorUserId,
    string AuthorRole,
    string AuthorName,
    string MessageType,
    string MessageText,
    bool IsInternal,
    string? MetadataJson,
    DateTime CreatedAtUtc);

public record AdminSupportTicketDetailsDto(
    AdminSupportTicketSummaryDto Ticket,
    string? MetadataJson,
    IReadOnlyList<AdminSupportTicketMessageDto> Messages);

public record AdminSupportTicketListResponseDto(
    IReadOnlyList<AdminSupportTicketSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    AdminSupportTicketQueueIndicatorsDto Indicators);

public record AdminSupportTicketOperationResultDto(
    bool Success,
    AdminSupportTicketDetailsDto? Ticket = null,
    AdminSupportTicketMessageDto? Message = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record AdminSupportTicketMessageRequestDto(
    string Message,
    bool IsInternal = false,
    string? MessageType = null,
    string? MetadataJson = null);

public record AdminSupportTicketStatusUpdateRequestDto(
    string Status,
    string? Note = null);

public record AdminSupportTicketAssignRequestDto(
    Guid? AssignedAdminUserId,
    string? Note = null);
