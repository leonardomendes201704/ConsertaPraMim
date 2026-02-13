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

    Task<AdminApiResult<AdminUpdateUserStatusResultDto>> UpdateUserStatusAsync(
        Guid userId,
        bool isActive,
        string? reason,
        string accessToken,
        CancellationToken cancellationToken = default);
}
