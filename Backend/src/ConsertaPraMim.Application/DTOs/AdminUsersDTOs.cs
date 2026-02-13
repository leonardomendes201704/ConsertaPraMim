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
