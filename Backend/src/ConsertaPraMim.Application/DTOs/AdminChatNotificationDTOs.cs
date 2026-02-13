namespace ConsertaPraMim.Application.DTOs;

public record AdminChatsQueryDto(
    Guid? RequestId,
    Guid? ProviderId,
    Guid? ClientId,
    string? SearchTerm,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page = 1,
    int PageSize = 20);

public record AdminChatConversationListItemDto(
    Guid RequestId,
    Guid ProviderId,
    string RequestDescription,
    string RequestStatus,
    string ClientName,
    string ClientEmailMasked,
    string ClientPhoneMasked,
    string ProviderName,
    string ProviderEmailMasked,
    string ProviderPhoneMasked,
    DateTime LastMessageAt,
    string LastMessageSenderRole,
    string LastMessagePreview,
    int MessagesCount,
    int AttachmentsCount);

public record AdminChatsListResponseDto(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<AdminChatConversationListItemDto> Items);

public record AdminChatMessageAttachmentDto(
    Guid Id,
    string FileUrl,
    string FileName,
    string ContentType,
    long SizeBytes,
    string MediaKind,
    DateTime CreatedAt);

public record AdminChatMessageDto(
    Guid Id,
    Guid SenderId,
    string SenderName,
    string SenderRole,
    string? Text,
    DateTime CreatedAt,
    IReadOnlyList<AdminChatMessageAttachmentDto> Attachments);

public record AdminChatDetailsDto(
    Guid RequestId,
    Guid ProviderId,
    string RequestDescription,
    string RequestStatus,
    string ClientName,
    string ClientEmailMasked,
    string ClientPhoneMasked,
    string ProviderName,
    string ProviderEmailMasked,
    string ProviderPhoneMasked,
    IReadOnlyList<AdminChatMessageDto> Messages);

public record AdminChatAttachmentsQueryDto(
    Guid? RequestId,
    Guid? UserId,
    string? MediaKind,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page = 1,
    int PageSize = 20);

public record AdminChatAttachmentListItemDto(
    Guid AttachmentId,
    Guid MessageId,
    Guid RequestId,
    Guid ProviderId,
    Guid SenderId,
    string SenderName,
    string SenderRole,
    string RequestDescription,
    string FileUrl,
    string FileName,
    string ContentType,
    long SizeBytes,
    string MediaKind,
    DateTime CreatedAt);

public record AdminChatAttachmentsListResponseDto(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<AdminChatAttachmentListItemDto> Items);

public record AdminSendNotificationRequestDto(
    Guid RecipientUserId,
    string Subject,
    string Message,
    string? ActionUrl,
    string? Reason);

public record AdminSendNotificationResultDto(
    bool Success,
    string? ErrorCode = null,
    string? ErrorMessage = null);
