using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;

namespace ConsertaPraMim.Web.Admin.Services;

public interface IAdminMailboxApiClient
{
    Task<AdminApiResult<AdminMailboxSettingsDto>> GetSettingsAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminOperationResultDto>> UpsertSettingsAsync(
        AdminMailboxUpsertSettingsRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<IReadOnlyList<AdminMailboxRecipientDto>>> GetRecipientsAsync(
        string? role,
        string? search,
        int take,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminMailboxListResponseDto>> GetMessagesAsync(
        AdminMailboxListQueryDto query,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminMailboxMessageDetailsDto>> GetMessageByIdAsync(
        string messageId,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminMailboxMessageDetailsDto>> MarkMessageReadAsync(
        string messageId,
        bool isRead,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminMailboxMessageDetailsDto>> SendAsync(
        AdminMailboxSendRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminMailboxSyncResultDto>> SyncAsync(
        string accessToken,
        CancellationToken cancellationToken = default);
}
