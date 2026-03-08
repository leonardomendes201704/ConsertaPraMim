using System.Text.Json.Serialization;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public record CaptureLandingLeadRequestDto(
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    LandingLeadOrigin Origin,
    string FullName,
    string Phone,
    string Email,
    string City,
    string State,
    string Neighborhood,
    string? ServiceCategory,
    string? RequestedService,
    string? CompanyName,
    string? CompanyDocument,
    int? YearsOfExperience,
    string? Message,
    string? CurrentPageUrl,
    string? ReferrerUrl,
    string? QueryString,
    string? UtmSource,
    string? UtmMedium,
    string? UtmCampaign,
    string? UtmTerm,
    string? UtmContent,
    string? BrowserLanguage,
    string? ScreenResolution,
    string? DevicePlatform,
    string? TimeZone);

public record LandingLeadCaptureContextDto(
    string? IpAddress,
    string? ForwardedFor,
    string? UserAgent,
    string? AcceptLanguage,
    string? Host,
    string? Scheme,
    string? Path,
    string? RefererHeader);

public record CaptureLandingLeadResponseDto(
    Guid LeadId,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    LandingLeadOrigin Origin,
    string Message,
    DateTime CapturedAtUtc);
