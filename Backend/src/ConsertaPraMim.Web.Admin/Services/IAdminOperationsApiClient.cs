using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;

namespace ConsertaPraMim.Web.Admin.Services;

public interface IAdminOperationsApiClient
{
    Task<AdminApiResult<AdminServiceRequestsListResponseDto>> GetServiceRequestsAsync(
        AdminServiceRequestsFilterModel filters,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminServiceRequestDetailsDto>> GetServiceRequestByIdAsync(
        Guid requestId,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminOperationResultDto>> UpdateServiceRequestStatusAsync(
        Guid requestId,
        string status,
        string? reason,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminProposalsListResponseDto>> GetProposalsAsync(
        AdminProposalsFilterModel filters,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminOperationResultDto>> InvalidateProposalAsync(
        Guid proposalId,
        string? reason,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminChatsListResponseDto>> GetChatsAsync(
        AdminChatsFilterModel filters,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminChatDetailsDto>> GetChatAsync(
        Guid requestId,
        Guid providerId,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminChatAttachmentsListResponseDto>> GetChatAttachmentsAsync(
        AdminChatAttachmentsFilterModel filters,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminSendNotificationResultDto>> SendNotificationAsync(
        Guid recipientUserId,
        string subject,
        string message,
        string? actionUrl,
        string? reason,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<Guid>> FindUserIdByEmailAsync(
        string email,
        string accessToken,
        CancellationToken cancellationToken = default);
}
