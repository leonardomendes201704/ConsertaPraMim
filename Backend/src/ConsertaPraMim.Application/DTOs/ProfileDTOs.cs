using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public record UserProfileDto(
    string Name, 
    string Email, 
    string Phone, 
    string Role,
    ClientProfileType ClientProfileType,
    ClientPjType? ClientPjType,
    string? ClientBaseZipCode,
    string? ClientBaseStreet,
    string? ClientBaseCity,
    double? ClientBaseLatitude,
    double? ClientBaseLongitude,
    string? ProfilePictureUrl,
    ProviderProfileDto? ProviderProfile);

public record ProviderProfileDto(
    ProviderPlan Plan,
    ProviderOnboardingStatus OnboardingStatus,
    bool IsOnboardingCompleted,
    int OnboardingDocumentsCount,
    double RadiusKm, 
    string? BaseZipCode,
    double? BaseLatitude, 
    double? BaseLongitude, 
    ProviderOperationalStatus OperationalStatus,
    ProviderClientPreference ClientPreference,
    List<ServiceCategory> Categories,
    double Rating,
    int ReviewCount,
    bool HasOperationalCompliancePending,
    string? OperationalComplianceNotes,
    double? PlanMaxRadiusKm,
    int? PlanMaxAllowedCategories,
    List<ServiceCategory> PlanAllowedCategories,
    ProviderTrustStatus TrustStatus = ProviderTrustStatus.Pending,
    ProviderRiskLevel RiskLevel = ProviderRiskLevel.Low,
    DateTime? TrustStatusUpdatedAtUtc = null,
    string? TrustStatusReason = null);

public record UpdateProviderProfileDto(
    double RadiusKm, 
    string? BaseZipCode,
    double? BaseLatitude, 
    double? BaseLongitude, 
    List<ServiceCategory> Categories,
    ProviderOperationalStatus? OperationalStatus = null,
    ProviderClientPreference? ClientPreference = null);

public record UpdateProviderOperationalStatusDto(ProviderOperationalStatus OperationalStatus);

public record UpdateUserProfileDto(
    string Name,
    int? ClientProfileType = null,
    int? ClientPjType = null,
    string? ClientBaseZipCode = null,
    string? ClientBaseStreet = null,
    string? ClientBaseCity = null,
    double? ClientBaseLatitude = null,
    double? ClientBaseLongitude = null);

public record UpdateProfilePictureDto(string? ImageUrl);

public record UserProfileLegalTermsStatusDto(
    string Audience,
    int ActiveVersion,
    string Title,
    string HtmlContent,
    DateTime PublishedAtUtc,
    bool Accepted,
    DateTime? AcceptedAtUtc,
    string? AcceptanceSource);

public record AcceptUserProfileLegalTermsDto(
    bool Accepted,
    string? Source = null);

public record UserProfileLegalTermsAcceptanceResultDto(
    bool Success,
    UserProfileLegalTermsStatusDto? Status = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);
