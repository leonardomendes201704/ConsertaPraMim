using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminUserService
{
    Task<AdminUsersListResponseDto> GetUsersAsync(AdminUsersQueryDto query);
    Task<AdminUserDetailsDto?> GetByIdAsync(Guid userId);
    Task<AdminCreateAdminUserResultDto> CreateAdminUserAsync(
        AdminCreateAdminUserRequestDto request,
        Guid actorUserId,
        string actorEmail);
    Task<AdminUpdateUserStatusResultDto> UpdateStatusAsync(
        Guid targetUserId,
        AdminUpdateUserStatusRequestDto request,
        Guid actorUserId,
        string actorEmail);
    Task<AdminProviderTrustQueueResponseDto> GetProviderTrustQueueAsync(AdminProviderTrustQueueQueryDto query);
    Task<IReadOnlyList<AdminProviderTrustReviewHistoryItemDto>> GetProviderTrustHistoryAsync(Guid providerUserId, int take = 30);
    Task<AdminProviderTrustReviewResultDto> ReviewProviderTrustAsync(
        Guid providerUserId,
        AdminProviderTrustReviewRequestDto request,
        Guid actorUserId,
        string actorEmail);
}
