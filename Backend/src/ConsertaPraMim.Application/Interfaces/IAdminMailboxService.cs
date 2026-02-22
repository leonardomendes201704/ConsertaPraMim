using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminMailboxService
{
    Task<AdminMailboxSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<AdminMailboxOperationResultDto> UpsertSettingsAsync(
        AdminMailboxUpsertSettingsRequestDto request,
        Guid actorUserId,
        string? actorEmail,
        CancellationToken cancellationToken = default);

    Task<AdminMailboxListResponseDto> GetMessagesAsync(
        AdminMailboxListQueryDto query,
        CancellationToken cancellationToken = default);

    Task<AdminMailboxOperationResultDto> GetMessageByIdAsync(
        string messageId,
        CancellationToken cancellationToken = default);

    Task<AdminMailboxOperationResultDto> MarkMessageReadAsync(
        string messageId,
        bool isRead,
        CancellationToken cancellationToken = default);

    Task<AdminMailboxOperationResultDto> SendAsync(
        AdminMailboxSendRequestDto request,
        Guid actorUserId,
        string? actorEmail,
        CancellationToken cancellationToken = default);

    Task<AdminMailboxSyncResultDto> SyncInboxAsync(
        bool notifyAdmins,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminMailboxRecipientDto>> GetRecipientsAsync(
        string? role,
        string? search,
        int take,
        CancellationToken cancellationToken = default);
}
