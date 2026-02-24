using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public record AdminUsersQueryDto(
    string? SearchTerm,
    string? Role,
    bool? IsActive,
    int Page = 1,
    int PageSize = 20);

public record AdminUserListItemDto(
    Guid Id,
    string Name,
    string Email,
    string Phone,
    string Role,
    bool IsActive,
    DateTime CreatedAt);

public record AdminUsersListResponseDto(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<AdminUserListItemDto> Items);

public record AdminProviderProfileSummaryDto(
    double RadiusKm,
    string? BaseZipCode,
    double? BaseLatitude,
    double? BaseLongitude,
    IReadOnlyList<ServiceCategory> Categories,
    bool IsVerified,
    ProviderTrustStatus TrustStatus,
    ProviderRiskLevel RiskLevel,
    DateTime? TrustStatusUpdatedAtUtc,
    string? TrustStatusReason,
    double Rating,
    int ReviewCount);

public record AdminUserDetailsDto(
    Guid Id,
    string Name,
    string Email,
    string Phone,
    string Role,
    bool IsActive,
    string? ProfilePictureUrl,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    AdminProviderProfileSummaryDto? ProviderProfile);

public record AdminUpdateUserStatusRequestDto(
    bool IsActive,
    string? Reason);

public record AdminUpdateUserStatusResultDto(
    bool Success,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record AdminProviderTrustQueueQueryDto(
    string? TrustStatus,
    string? RiskLevel,
    int Take = 100);

public record AdminProviderTrustQueueItemDto(
    Guid ProviderUserId,
    Guid ProviderProfileId,
    string ProviderName,
    string ProviderEmail,
    ProviderTrustStatus TrustStatus,
    ProviderRiskLevel RiskLevel,
    bool IsVerified,
    DateTime? TrustStatusUpdatedAtUtc,
    string? TrustStatusReason,
    int PendingDocuments,
    int RejectedDocuments,
    int ApprovedDocuments,
    DateTime CreatedAtUtc);

public record AdminProviderTrustQueueResponseDto(
    int TotalCount,
    IReadOnlyList<AdminProviderTrustQueueItemDto> Items);

public record AdminProviderTrustReviewRequestDto(
    ProviderTrustStatus TrustStatus,
    ProviderRiskLevel RiskLevel,
    string? DecisionReason,
    string? EvidenceSummary);

public record AdminProviderTrustReviewHistoryItemDto(
    Guid Id,
    ProviderTrustStatus PreviousTrustStatus,
    ProviderTrustStatus NewTrustStatus,
    ProviderRiskLevel PreviousRiskLevel,
    ProviderRiskLevel NewRiskLevel,
    string? DecisionReason,
    string? EvidenceSummary,
    Guid ReviewedByAdminUserId,
    string ReviewedByAdminEmail,
    DateTime ReviewedAtUtc);

public record AdminProviderTrustReviewResultDto(
    bool Success,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    ProviderTrustStatus? AppliedTrustStatus = null,
    ProviderRiskLevel? AppliedRiskLevel = null);
