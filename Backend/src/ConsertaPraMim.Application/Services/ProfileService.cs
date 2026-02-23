using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using System.Text.Json;

namespace ConsertaPraMim.Application.Services;

public class ProfileService : IProfileService
{
    private readonly IUserRepository _userRepository;
    private readonly IPlanGovernanceService _planGovernanceService;
    private readonly ILegalTermsRepository _legalTermsRepository;

    public ProfileService(
        IUserRepository userRepository,
        IPlanGovernanceService planGovernanceService,
        ILegalTermsRepository legalTermsRepository)
    {
        _userRepository = userRepository;
        _planGovernanceService = planGovernanceService;
        _legalTermsRepository = legalTermsRepository;
    }

    public async Task<UserProfileDto?> GetProfileAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null) return null;

        ProviderProfileDto? providerDto = null;
        if (user.ProviderProfile != null)
        {
            var planRules = await _planGovernanceService.GetOperationalRulesAsync(user.ProviderProfile.Plan);
            var hasCompliancePending = user.ProviderProfile.HasOperationalCompliancePending;
            var complianceNotes = user.ProviderProfile.OperationalComplianceNotes;
            if (planRules != null)
            {
                var runtimeValidation = await _planGovernanceService.ValidateOperationalSelectionAsync(
                    user.ProviderProfile.Plan,
                    user.ProviderProfile.RadiusKm,
                    user.ProviderProfile.Categories);
                if (!runtimeValidation.Success)
                {
                    hasCompliancePending = true;
                    complianceNotes = runtimeValidation.ErrorMessage;
                }
            }

            providerDto = new ProviderProfileDto(
                user.ProviderProfile.Plan,
                user.ProviderProfile.OnboardingStatus,
                user.ProviderProfile.IsOnboardingCompleted,
                user.ProviderProfile.OnboardingDocuments.Count,
                user.ProviderProfile.RadiusKm,
                user.ProviderProfile.BaseZipCode,
                user.ProviderProfile.BaseLatitude,
                user.ProviderProfile.BaseLongitude,
                user.ProviderProfile.OperationalStatus,
                user.ProviderProfile.ClientPreference,
                user.ProviderProfile.Categories,
                user.ProviderProfile.Rating,
                user.ProviderProfile.ReviewCount,
                hasCompliancePending,
                complianceNotes,
                planRules?.MaxRadiusKm,
                planRules?.MaxAllowedCategories,
                planRules?.AllowedCategories?.ToList() ?? new List<ServiceCategory>());
        }

        return new UserProfileDto(
            user.Name,
            user.Email,
            user.Phone,
            user.Role.ToString(),
            user.ClientProfileType,
            user.ClientPjType,
            user.ClientBaseZipCode,
            user.ClientBaseStreet,
            user.ClientBaseCity,
            user.ClientBaseLatitude,
            user.ClientBaseLongitude,
            user.ProfilePictureUrl,
            providerDto);
    }

    public async Task<bool> UpdateUserProfileAsync(Guid userId, UpdateUserProfileDto dto)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null) return false;

        var normalizedName = dto.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 120)
        {
            return false;
        }

        if (user.Role == UserRole.Client)
        {
            var targetClientProfileType = user.ClientProfileType;
            if (dto.ClientProfileType.HasValue)
            {
                if (!Enum.IsDefined(typeof(ClientProfileType), dto.ClientProfileType.Value))
                {
                    return false;
                }

                targetClientProfileType = (ClientProfileType)dto.ClientProfileType.Value;
            }

            ClientPjType? targetClientPjType = user.ClientPjType;
            if (targetClientProfileType == ClientProfileType.Pj)
            {
                if (dto.ClientPjType.HasValue)
                {
                    if (!Enum.IsDefined(typeof(ClientPjType), dto.ClientPjType.Value))
                    {
                        return false;
                    }

                    targetClientPjType = (ClientPjType)dto.ClientPjType.Value;
                }

                if (!targetClientPjType.HasValue)
                {
                    return false;
                }
            }
            else
            {
                if (dto.ClientPjType.HasValue)
                {
                    return false;
                }

                targetClientPjType = null;
            }

            user.ClientProfileType = targetClientProfileType;
            user.ClientPjType = targetClientPjType;

            var hasAnyClientLocationPayload =
                !string.IsNullOrWhiteSpace(dto.ClientBaseZipCode) ||
                !string.IsNullOrWhiteSpace(dto.ClientBaseStreet) ||
                !string.IsNullOrWhiteSpace(dto.ClientBaseCity) ||
                dto.ClientBaseLatitude.HasValue ||
                dto.ClientBaseLongitude.HasValue;

            if (hasAnyClientLocationPayload)
            {
                if (dto.ClientBaseLatitude.HasValue != dto.ClientBaseLongitude.HasValue)
                {
                    return false;
                }

                if (dto.ClientBaseLatitude is double latitude && (latitude < -90 || latitude > 90))
                {
                    return false;
                }

                if (dto.ClientBaseLongitude is double longitude && (longitude < -180 || longitude > 180))
                {
                    return false;
                }

                var normalizedZip = NormalizeZip(dto.ClientBaseZipCode);
                if (!string.IsNullOrEmpty(normalizedZip) && normalizedZip.Length != 8)
                {
                    return false;
                }

                user.ClientBaseZipCode = string.IsNullOrEmpty(normalizedZip) ? null : normalizedZip;
                user.ClientBaseStreet = NormalizeOptionalText(dto.ClientBaseStreet);
                user.ClientBaseCity = NormalizeOptionalText(dto.ClientBaseCity);
                user.ClientBaseLatitude = dto.ClientBaseLatitude;
                user.ClientBaseLongitude = dto.ClientBaseLongitude;
            }
        }

        user.Name = normalizedName;
        await _userRepository.UpdateAsync(user);
        return true;
    }

    public async Task<bool> UpdateProviderProfileAsync(Guid userId, UpdateProviderProfileDto dto)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || user.Role != UserRole.Provider) return false;

        if (user.ProviderProfile == null)
        {
            user.ProviderProfile = new ProviderProfile { UserId = userId };
        }

        var categories = (dto.Categories ?? new List<ServiceCategory>())
            .Distinct()
            .ToList();
        var validation = await _planGovernanceService.ValidateOperationalSelectionAsync(
            user.ProviderProfile.Plan,
            dto.RadiusKm,
            categories);
        if (!validation.Success)
        {
            return false;
        }

        user.ProviderProfile.RadiusKm = dto.RadiusKm;
        user.ProviderProfile.BaseZipCode = dto.BaseZipCode;
        if (dto.OperationalStatus.HasValue)
        {
            user.ProviderProfile.OperationalStatus = dto.OperationalStatus.Value;
        }
        if (dto.ClientPreference.HasValue)
        {
            user.ProviderProfile.ClientPreference = dto.ClientPreference.Value;
        }

        if (dto.BaseLatitude.HasValue && dto.BaseLongitude.HasValue)
        {
            user.ProviderProfile.BaseLatitude = dto.BaseLatitude;
            user.ProviderProfile.BaseLongitude = dto.BaseLongitude;
        }

        user.ProviderProfile.Categories = categories;
        user.ProviderProfile.HasOperationalCompliancePending = false;
        user.ProviderProfile.OperationalComplianceNotes = null;

        await _userRepository.UpdateAsync(user);
        return true;
    }

    public async Task<bool> UpdateProviderOperationalStatusAsync(Guid userId, ProviderOperationalStatus status)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || user.Role != UserRole.Provider) return false;

        if (user.ProviderProfile == null)
        {
            user.ProviderProfile = new ProviderProfile { UserId = userId };
        }

        user.ProviderProfile.OperationalStatus = status;
        await _userRepository.UpdateAsync(user);
        return true;
    }

    public async Task<ProviderOperationalStatus?> GetProviderOperationalStatusAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || user.Role != UserRole.Provider || user.ProviderProfile == null)
        {
            return null;
        }

        return user.ProviderProfile.OperationalStatus;
    }

    public async Task<bool> UpdateProfilePictureAsync(Guid userId, string imageUrl)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null) return false;

        user.ProfilePictureUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        await _userRepository.UpdateAsync(user);
        return true;
    }

    public async Task<UserProfileLegalTermsStatusDto?> GetLegalTermsStatusAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || !TryResolveAudience(user.Role, out var audience))
        {
            return null;
        }

        var activeTerms = await _legalTermsRepository.GetActiveByAudienceAsync(audience);
        if (activeTerms == null || !activeTerms.IsPublished)
        {
            return null;
        }

        var latestAcceptance = await _legalTermsRepository.GetLatestAcceptanceByUserAsync(userId, audience);
        var hasAcceptedActiveVersion = latestAcceptance != null &&
                                       latestAcceptance.LegalTermsDocumentId == activeTerms.Id &&
                                       latestAcceptance.TermsVersion == activeTerms.Version;

        return new UserProfileLegalTermsStatusDto(
            Audience: LegalTermsService.ToAudienceKey(audience),
            ActiveVersion: activeTerms.Version,
            Title: activeTerms.Title,
            HtmlContent: activeTerms.HtmlContent,
            PublishedAtUtc: activeTerms.PublishedAtUtc ?? activeTerms.CreatedAt,
            Accepted: hasAcceptedActiveVersion,
            AcceptedAtUtc: hasAcceptedActiveVersion ? latestAcceptance!.AcceptedAtUtc : null,
            AcceptanceSource: hasAcceptedActiveVersion ? latestAcceptance!.Source : null);
    }

    public async Task<UserProfileLegalTermsAcceptanceResultDto> AcceptLegalTermsAsync(Guid userId, string? source = null)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return new UserProfileLegalTermsAcceptanceResultDto(
                Success: false,
                ErrorCode: "profile_terms_user_not_found",
                ErrorMessage: "Usuario nao encontrado.");
        }

        if (!TryResolveAudience(user.Role, out var audience))
        {
            return new UserProfileLegalTermsAcceptanceResultDto(
                Success: false,
                ErrorCode: "profile_terms_audience_not_supported",
                ErrorMessage: "Termos disponiveis apenas para clientes e prestadores.");
        }

        var activeTerms = await _legalTermsRepository.GetActiveByAudienceAsync(audience);
        if (activeTerms == null || !activeTerms.IsPublished)
        {
            return new UserProfileLegalTermsAcceptanceResultDto(
                Success: false,
                ErrorCode: "profile_terms_not_found",
                ErrorMessage: "Nenhum termo ativo encontrado para este perfil.");
        }

        var existingAcceptance = await _legalTermsRepository.GetAcceptanceByUserAndDocumentAsync(userId, activeTerms.Id);
        if (existingAcceptance == null)
        {
            var normalizedSource = NormalizeTermsAcceptanceSource(source, user.Role);
            await _legalTermsRepository.AddAcceptanceAsync(new UserLegalTermsAcceptance
            {
                UserId = user.Id,
                LegalTermsDocumentId = activeTerms.Id,
                Audience = audience,
                TermsVersion = activeTerms.Version,
                AcceptedAtUtc = DateTime.UtcNow,
                Source = normalizedSource,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    termsType = LegalTermsService.ToAudienceKey(audience),
                    termsVersion = activeTerms.Version,
                    channel = "profile"
                })
            });

            await _legalTermsRepository.SaveChangesAsync();
        }

        var status = await GetLegalTermsStatusAsync(userId);
        if (status == null)
        {
            return new UserProfileLegalTermsAcceptanceResultDto(
                Success: false,
                ErrorCode: "profile_terms_status_unavailable",
                ErrorMessage: "Nao foi possivel obter o status atualizado do termo.");
        }

        return new UserProfileLegalTermsAcceptanceResultDto(
            Success: true,
            Status: status,
            ErrorCode: null,
            ErrorMessage: null);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string NormalizeZip(string? zipCode)
    {
        if (string.IsNullOrWhiteSpace(zipCode))
        {
            return string.Empty;
        }

        return new string(zipCode.Where(char.IsDigit).ToArray());
    }

    private static bool TryResolveAudience(UserRole role, out LegalTermsAudience audience)
    {
        switch (role)
        {
            case UserRole.Client:
                audience = LegalTermsAudience.Client;
                return true;
            case UserRole.Provider:
                audience = LegalTermsAudience.Provider;
                return true;
            default:
                audience = LegalTermsAudience.Client;
                return false;
        }
    }

    private static string NormalizeTermsAcceptanceSource(string? source, UserRole role)
    {
        var fallback = role == UserRole.Client
            ? "mobile_client_profile"
            : "mobile_provider_profile";

        if (string.IsNullOrWhiteSpace(source))
        {
            return fallback;
        }

        var trimmed = source.Trim();
        return trimmed.Length <= 60 ? trimmed : trimmed[..60];
    }
}
