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
    private readonly IProviderTrustReviewRepository _providerTrustReviewRepository;
    private readonly IAdminAuditLogRepository _adminAuditLogRepository;
    private readonly ILogger<AdminUserService> _logger;

    public AdminUserService(
        IUserRepository userRepository,
        IProviderTrustReviewRepository providerTrustReviewRepository,
        IAdminAuditLogRepository adminAuditLogRepository,
        ILogger<AdminUserService>? logger = null)
    {
        _userRepository = userRepository;
        _providerTrustReviewRepository = providerTrustReviewRepository;
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

    public async Task<AdminProviderTrustQueueResponseDto> GetProviderTrustQueueAsync(AdminProviderTrustQueueQueryDto query)
    {
        var trustStatus = ParseTrustStatus(query.TrustStatus);
        var riskLevel = ParseRiskLevel(query.RiskLevel);
        var take = Math.Clamp(query.Take <= 0 ? 100 : query.Take, 1, 300);

        var queue = await _providerTrustReviewRepository.GetQueueAsync(trustStatus, riskLevel, take);
        var items = queue.Select(profile =>
            new AdminProviderTrustQueueItemDto(
                profile.UserId,
                profile.Id,
                profile.User.Name,
                profile.User.Email,
                profile.TrustStatus,
                profile.RiskLevel,
                profile.IsVerified,
                profile.TrustStatusUpdatedAtUtc,
                profile.TrustStatusReason,
                profile.OnboardingDocuments.Count(doc => doc.Status == ProviderDocumentStatus.Pending),
                profile.OnboardingDocuments.Count(doc => doc.Status == ProviderDocumentStatus.Rejected),
                profile.OnboardingDocuments.Count(doc => doc.Status == ProviderDocumentStatus.Approved),
                profile.CreatedAt))
            .ToList();

        return new AdminProviderTrustQueueResponseDto(items.Count, items);
    }

    public async Task<IReadOnlyList<AdminProviderTrustReviewHistoryItemDto>> GetProviderTrustHistoryAsync(Guid providerUserId, int take = 30)
    {
        var history = await _providerTrustReviewRepository.GetByProviderUserIdAsync(providerUserId, take);
        return history
            .Select(item => new AdminProviderTrustReviewHistoryItemDto(
                item.Id,
                item.PreviousTrustStatus,
                item.NewTrustStatus,
                item.PreviousRiskLevel,
                item.NewRiskLevel,
                item.DecisionReason,
                item.EvidenceSummary,
                item.ReviewedByAdminUserId,
                item.ReviewedByAdminEmail,
                item.ReviewedAtUtc))
            .ToList();
    }

    public async Task<AdminProviderTrustReviewResultDto> ReviewProviderTrustAsync(
        Guid providerUserId,
        AdminProviderTrustReviewRequestDto request,
        Guid actorUserId,
        string actorEmail)
    {
        var providerUser = await _userRepository.GetByIdAsync(providerUserId);
        if (providerUser?.ProviderProfile == null)
        {
            return new AdminProviderTrustReviewResultDto(
                false,
                "provider_not_found",
                "Prestador nao encontrado para revisao de confianca.");
        }

        var profile = providerUser.ProviderProfile;
        var previousTrustStatus = profile.TrustStatus;
        var previousRiskLevel = profile.RiskLevel;
        var previousIsVerified = profile.IsVerified;
        var normalizedReason = string.IsNullOrWhiteSpace(request.DecisionReason)
            ? "Atualizacao manual de status de confianca."
            : request.DecisionReason.Trim();
        var normalizedEvidence = string.IsNullOrWhiteSpace(request.EvidenceSummary)
            ? null
            : request.EvidenceSummary.Trim();

        profile.TrustStatus = request.TrustStatus;
        profile.RiskLevel = request.RiskLevel;
        profile.IsVerified = request.TrustStatus == ProviderTrustStatus.Verified;
        profile.TrustStatusUpdatedAtUtc = DateTime.UtcNow;
        profile.TrustStatusReason = normalizedReason;
        profile.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(providerUser);

        await _providerTrustReviewRepository.AddAsync(new ProviderTrustReview
        {
            ProviderProfileId = profile.Id,
            ProviderUserId = providerUserId,
            PreviousTrustStatus = previousTrustStatus,
            NewTrustStatus = request.TrustStatus,
            PreviousRiskLevel = previousRiskLevel,
            NewRiskLevel = request.RiskLevel,
            DecisionReason = normalizedReason,
            EvidenceSummary = normalizedEvidence,
            ReviewedByAdminUserId = actorUserId,
            ReviewedByAdminEmail = actorEmail,
            ReviewedAtUtc = DateTime.UtcNow
        });

        var metadata = JsonSerializer.Serialize(new
        {
            before = new
            {
                trustStatus = previousTrustStatus.ToString(),
                riskLevel = previousRiskLevel.ToString(),
                isVerified = previousIsVerified
            },
            after = new
            {
                trustStatus = request.TrustStatus.ToString(),
                riskLevel = request.RiskLevel.ToString(),
                isVerified = profile.IsVerified
            },
            reason = normalizedReason,
            evidence = normalizedEvidence
        });

        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = actorUserId,
            ActorEmail = actorEmail,
            Action = "ProviderTrustReviewed",
            TargetType = "ProviderProfile",
            TargetId = profile.Id,
            Metadata = metadata
        });

        _logger.LogInformation(
            "Admin provider trust reviewed. ActorUserId={ActorUserId}, ProviderUserId={ProviderUserId}, PreviousTrustStatus={PreviousTrustStatus}, NewTrustStatus={NewTrustStatus}, PreviousRiskLevel={PreviousRiskLevel}, NewRiskLevel={NewRiskLevel}",
            actorUserId,
            providerUserId,
            previousTrustStatus,
            request.TrustStatus,
            previousRiskLevel,
            request.RiskLevel);

        return new AdminProviderTrustReviewResultDto(
            true,
            AppliedTrustStatus: request.TrustStatus,
            AppliedRiskLevel: request.RiskLevel);
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
                user.ProviderProfile.TrustStatus,
                user.ProviderProfile.RiskLevel,
                user.ProviderProfile.TrustStatusUpdatedAtUtc,
                user.ProviderProfile.TrustStatusReason,
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

    private static ProviderTrustStatus? ParseTrustStatus(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return Enum.TryParse<ProviderTrustStatus>(rawValue.Trim(), true, out var parsed)
            ? parsed
            : null;
    }

    private static ProviderRiskLevel? ParseRiskLevel(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return Enum.TryParse<ProviderRiskLevel>(rawValue.Trim(), true, out var parsed)
            ? parsed
            : null;
    }
}
