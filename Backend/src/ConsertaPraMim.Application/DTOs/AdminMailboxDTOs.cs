namespace ConsertaPraMim.Application.DTOs;

public record AdminMailboxSettingsDto(
    bool IsConfigured,
    bool Enabled,
    string SenderDisplayName,
    string SenderEmail,
    string Username,
    bool HasPassword,
    string SmtpHost,
    int SmtpPort,
    bool SmtpUseSsl,
    string Pop3Host,
    int Pop3Port,
    bool Pop3UseSsl,
    int SyncWindowSize,
    int PollIntervalSeconds,
    DateTime? LastSyncAtUtc,
    string? LastSyncStatus,
    string? LastSyncError);

public record AdminMailboxUpsertSettingsRequestDto(
    bool Enabled,
    string SenderDisplayName,
    string SenderEmail,
    string Username,
    string? Password,
    string SmtpHost,
    int SmtpPort,
    bool SmtpUseSsl,
    string Pop3Host,
    int Pop3Port,
    bool Pop3UseSsl,
    int SyncWindowSize = 40,
    int PollIntervalSeconds = 120);

public record AdminMailboxListQueryDto(
    string? Folder = "inbox",
    string? Search = null,
    int Page = 1,
    int PageSize = 30);

public record AdminMailboxMessageSummaryDto(
    string Id,
    string Direction,
    string Subject,
    string FromAddress,
    string ToAddress,
    string Preview,
    DateTime OccurredAtUtc,
    bool IsRead,
    string? ExternalMessageId,
    int AttachmentsCount = 0);

public record AdminMailboxMessageDetailsDto(
    string Id,
    string Direction,
    string Subject,
    string FromAddress,
    string ToAddress,
    string Preview,
    string BodyText,
    string? BodyHtml,
    IReadOnlyList<AdminMailboxMessageAttachmentDto> Attachments,
    DateTime OccurredAtUtc,
    bool IsRead,
    string? ExternalMessageId);

public record AdminMailboxMessageAttachmentDto(
    string Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string ContentBase64,
    bool IsImage);

public record AdminMailboxListResponseDto(
    IReadOnlyList<AdminMailboxMessageSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    DateTime? LastSyncAtUtc,
    string? LastSyncStatus,
    string? LastSyncError);

public record AdminMailboxRecipientDto(
    Guid UserId,
    string Name,
    string Email,
    string Role,
    bool IsActive);

public record AdminMailboxSendRequestDto(
    string To,
    string Subject,
    string Body,
    bool IsHtml = false,
    IReadOnlyList<AdminMailboxAttachmentDto>? Attachments = null);

public record AdminMailboxAttachmentDto(
    string FileName,
    string? ContentType,
    string ContentBase64);

public record AdminMailboxMarkReadRequestDto(
    bool IsRead);

public record AdminMailboxOperationResultDto(
    bool Success,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    AdminMailboxMessageDetailsDto? Message = null);

public record AdminMailboxSyncResultDto(
    bool Success,
    int FetchedCount = 0,
    int NewMessagesCount = 0,
    DateTime? SyncedAtUtc = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);
