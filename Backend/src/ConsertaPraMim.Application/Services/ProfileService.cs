using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.Services;

public class ProfileService : IProfileService
{
    private readonly IUserRepository _userRepository;
    private readonly IPlanGovernanceService _planGovernanceService;

    public ProfileService(
        IUserRepository userRepository,
        IPlanGovernanceService planGovernanceService)
    {
        _userRepository = userRepository;
        _planGovernanceService = planGovernanceService;
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
            user.ProfilePictureUrl,
            providerDto);
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
}
