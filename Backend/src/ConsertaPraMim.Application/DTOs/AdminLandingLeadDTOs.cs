using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public record AdminLandingLeadsQueryDto(
    string? SearchTerm,
    string? Origin,
    string? City,
    string? State,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page = 1,
    int PageSize = 20);

public record AdminLandingLeadListItemDto(
    Guid Id,
    LandingLeadOrigin Origin,
    string FullName,
    string Phone,
    string Email,
    string Locality,
    string City,
    string State,
    string Neighborhood,
    string? PrimaryInterest,
    string? UtmCampaign,
    DateTime CreatedAtUtc);

public record AdminLandingLeadsListResponseDto(
    int Page,
    int PageSize,
    int TotalCount,
    int TotalClientLeads,
    int TotalProviderLeads,
    IReadOnlyList<AdminLandingLeadListItemDto> Items);

public record AdminLandingLeadDetailsDto(
    Guid Id,
    LandingLeadOrigin Origin,
    string FullName,
    string Phone,
    string Email,
    string City,
    string State,
    string Neighborhood,
    string Locality,
    string? ServiceCategory,
    string? RequestedService,
    string? CompanyName,
    string? CompanyDocument,
    int? YearsOfExperience,
    string? Message,
    string? CurrentPageUrl,
    string? ReferrerUrl,
    string? Host,
    string? Scheme,
    string? Path,
    string? QueryString,
    string? UtmSource,
    string? UtmMedium,
    string? UtmCampaign,
    string? UtmTerm,
    string? UtmContent,
    string? IpAddress,
    string? ForwardedFor,
    string? UserAgent,
    string? AcceptLanguage,
    string? BrowserLanguage,
    string? ScreenResolution,
    string? DevicePlatform,
    string? TimeZone,
    string? MetadataJson,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
