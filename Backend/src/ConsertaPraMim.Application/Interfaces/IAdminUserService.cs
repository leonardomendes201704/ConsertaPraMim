using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminUserService
{
    Task<AdminUsersListResponseDto> GetUsersAsync(AdminUsersQueryDto query);
    Task<AdminUserDetailsDto?> GetByIdAsync(Guid userId);
    Task<AdminUpdateUserStatusResultDto> UpdateStatusAsync(
        Guid targetUserId,
        AdminUpdateUserStatusRequestDto request,
        Guid actorUserId,
        string actorEmail);
}
