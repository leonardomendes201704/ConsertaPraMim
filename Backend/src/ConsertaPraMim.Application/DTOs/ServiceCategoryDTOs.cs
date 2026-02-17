namespace ConsertaPraMim.Application.DTOs;

public record ServiceCategoryOptionDto(
    Guid Id,
    string Name,
    string Slug,
    string LegacyCategory,
    string Icon);

public record AdminServiceCategoryDto(
    Guid Id,
    string Name,
    string Slug,
    string LegacyCategory,
    string Icon,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record AdminCreateServiceCategoryRequestDto(
    string Name,
    string? Slug,
    string LegacyCategory,
    string Icon);

public record AdminUpdateServiceCategoryRequestDto(
    string Name,
    string? Slug,
    string LegacyCategory,
    string Icon);

public record AdminUpdateServiceCategoryStatusRequestDto(
    bool IsActive,
    string? Reason);

public record AdminServiceCategoryUpsertResultDto(
    bool Success,
    AdminServiceCategoryDto? Category = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);
