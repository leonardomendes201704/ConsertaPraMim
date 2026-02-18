using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Web.Provider.Services;

public interface IProviderLegacyAdminApiClient
{
    Task<(AdminDashboardDto? Dashboard, string? ErrorMessage)> GetDashboardAsync(CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<AdminUserListItemDto> Users, int TotalCount, string? ErrorMessage)> GetUsersAsync(
        string? searchTerm = null,
        string? role = null,
        bool? isActive = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<(AdminUserDetailsDto? User, string? ErrorMessage)> GetUserByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? ErrorMessage)> UpdateUserStatusAsync(
        Guid userId,
        bool isActive,
        string? reason = null,
        CancellationToken cancellationToken = default);
}
