using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Models;

namespace ConsertaPraMim.Web.Admin.Services;

public interface IAdminUsersApiClient
{
    Task<AdminApiResult<AdminUsersListResponseDto>> GetUsersAsync(
        AdminUsersFilterModel filters,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminUserDetailsDto>> GetUserByIdAsync(
        Guid userId,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminCreateAdminUserResultDto>> CreateAdminUserAsync(
        AdminCreateAdminUserRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminUpdateUserStatusResultDto>> UpdateUserStatusAsync(
        Guid userId,
        bool isActive,
        string? reason,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminProviderTrustQueueResponseDto>> GetProviderTrustQueueAsync(
        string? trustStatus,
        string? riskLevel,
        int take,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<IReadOnlyList<AdminProviderTrustReviewHistoryItemDto>>> GetProviderTrustHistoryAsync(
        Guid providerUserId,
        int take,
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<AdminApiResult<AdminProviderTrustReviewResultDto>> ReviewProviderTrustAsync(
        Guid providerUserId,
        AdminProviderTrustReviewRequestDto request,
        string accessToken,
        CancellationToken cancellationToken = default);
}
