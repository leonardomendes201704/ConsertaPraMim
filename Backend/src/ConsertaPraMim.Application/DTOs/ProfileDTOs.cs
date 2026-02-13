using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public record UserProfileDto(
    string Name, 
    string Email, 
    string Phone, 
    string Role,
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
    List<ServiceCategory> Categories,
    double Rating,
    int ReviewCount,
    bool HasOperationalCompliancePending,
    string? OperationalComplianceNotes,
    double? PlanMaxRadiusKm,
    int? PlanMaxAllowedCategories,
    List<ServiceCategory> PlanAllowedCategories);

public record UpdateProviderProfileDto(
    double RadiusKm, 
    string? BaseZipCode,
    double? BaseLatitude, 
    double? BaseLongitude, 
    List<ServiceCategory> Categories,
    ProviderOperationalStatus? OperationalStatus = null);

public record UpdateProviderOperationalStatusDto(ProviderOperationalStatus OperationalStatus);
