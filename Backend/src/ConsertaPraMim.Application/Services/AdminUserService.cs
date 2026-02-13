using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace ConsertaPraMim.Application.Services;

public class AdminUserService : IAdminUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IAdminAuditLogRepository _adminAuditLogRepository;
    private readonly ILogger<AdminUserService> _logger;

    public AdminUserService(
        IUserRepository userRepository,
        IAdminAuditLogRepository adminAuditLogRepository,
        ILogger<AdminUserService>? logger = null)
    {
        _userRepository = userRepository;
        _adminAuditLogRepository = adminAuditLogRepository;
        _logger = logger ?? NullLogger<AdminUserService>.Instance;
    }

    public async Task<AdminUsersListResponseDto> GetUsersAsync(AdminUsersQueryDto query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);
        var users = (await _userRepository.GetAllAsync()).AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchTerm = query.SearchTerm.Trim();
            users = users.Where(u =>
                u.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                u.Email.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                u.Phone.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (query.IsActive.HasValue)
        {
            users = users.Where(u => u.IsActive == query.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Role) &&
            Enum.TryParse<UserRole>(query.Role, true, out var parsedRole))
        {
            users = users.Where(u => u.Role == parsedRole);
        }

        var ordered = users.OrderByDescending(u => u.CreatedAt).ToList();
        var totalCount = ordered.Count;
        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapListItem)
            .ToList();

        return new AdminUsersListResponseDto(page, pageSize, totalCount, items);
    }

    public async Task<AdminUserDetailsDto?> GetByIdAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        return user == null ? null : MapDetails(user);
    }

    public async Task<AdminUpdateUserStatusResultDto> UpdateStatusAsync(
        Guid targetUserId,
        AdminUpdateUserStatusRequestDto request,
        Guid actorUserId,
        string actorEmail)
    {
        var targetUser = await _userRepository.GetByIdAsync(targetUserId);
        if (targetUser == null)
        {
            _logger.LogWarning(
                "Admin user status change failed: target user not found. ActorUserId={ActorUserId}, TargetUserId={TargetUserId}",
                actorUserId,
                targetUserId);
            return new AdminUpdateUserStatusResultDto(false, "not_found", "Usuario nao encontrado.");
        }

        if (targetUser.Id == actorUserId && !request.IsActive)
        {
            _logger.LogWarning(
                "Admin user status change blocked: self-deactivation attempt. ActorUserId={ActorUserId}",
                actorUserId);
            return new AdminUpdateUserStatusResultDto(false, "self_deactivate_forbidden", "Nao e permitido desativar sua propria conta admin.");
        }

        if (targetUser.Role == UserRole.Admin && !request.IsActive)
        {
            var allUsers = await _userRepository.GetAllAsync();
            var activeAdminCount = allUsers.Count(u => u.Role == UserRole.Admin && u.IsActive);
            if (activeAdminCount <= 1)
            {
                _logger.LogWarning(
                    "Admin user status change blocked: last active admin. ActorUserId={ActorUserId}, TargetUserId={TargetUserId}",
                    actorUserId,
                    targetUserId);
                return new AdminUpdateUserStatusResultDto(false, "last_admin_forbidden", "Nao e permitido desativar o ultimo admin ativo.");
            }
        }

        var previousStatus = targetUser.IsActive;
        targetUser.IsActive = request.IsActive;
        targetUser.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(targetUser);

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "-" : request.Reason.Trim();
        var metadata = JsonSerializer.Serialize(new
        {
            before = new
            {
                isActive = previousStatus
            },
            after = new
            {
                isActive = request.IsActive
            },
            reason
        });

        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = actorUserId,
            ActorEmail = actorEmail,
            Action = "UserStatusChanged",
            TargetType = "User",
            TargetId = targetUserId,
            Metadata = metadata
        });

        _logger.LogInformation(
            "Admin user status changed. ActorUserId={ActorUserId}, TargetUserId={TargetUserId}, PreviousStatus={PreviousStatus}, NewStatus={NewStatus}",
            actorUserId,
            targetUserId,
            previousStatus,
            request.IsActive);

        return new AdminUpdateUserStatusResultDto(true);
    }

    private static AdminUserListItemDto MapListItem(User user)
    {
        return new AdminUserListItemDto(
            user.Id,
            user.Name,
            user.Email,
            user.Phone,
            user.Role.ToString(),
            user.IsActive,
            user.CreatedAt);
    }

    private static AdminUserDetailsDto MapDetails(User user)
    {
        AdminProviderProfileSummaryDto? providerProfile = null;
        if (user.ProviderProfile != null)
        {
            providerProfile = new AdminProviderProfileSummaryDto(
                user.ProviderProfile.RadiusKm,
                user.ProviderProfile.BaseZipCode,
                user.ProviderProfile.BaseLatitude,
                user.ProviderProfile.BaseLongitude,
                user.ProviderProfile.Categories,
                user.ProviderProfile.IsVerified,
                user.ProviderProfile.Rating,
                user.ProviderProfile.ReviewCount);
        }

        return new AdminUserDetailsDto(
            user.Id,
            user.Name,
            user.Email,
            user.Phone,
            user.Role.ToString(),
            user.IsActive,
            user.ProfilePictureUrl,
            user.CreatedAt,
            user.UpdatedAt,
            providerProfile);
    }
}
