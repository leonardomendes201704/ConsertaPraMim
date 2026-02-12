using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.Services;

public class ProfileService : IProfileService
{
    private readonly IUserRepository _userRepository;

    public ProfileService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserProfileDto?> GetProfileAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null) return null;

        ProviderProfileDto? providerDto = null;
        if (user.ProviderProfile != null)
        {
            providerDto = new ProviderProfileDto(
                user.ProviderProfile.RadiusKm,
                user.ProviderProfile.BaseLatitude,
                user.ProviderProfile.BaseLongitude,
                user.ProviderProfile.Categories,
                user.ProviderProfile.Rating,
                user.ProviderProfile.ReviewCount);
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

        user.ProviderProfile.RadiusKm = dto.RadiusKm;
        user.ProviderProfile.BaseLatitude = dto.BaseLatitude;
        user.ProviderProfile.BaseLongitude = dto.BaseLongitude;
        user.ProviderProfile.Categories = dto.Categories;

        await _userRepository.UpdateAsync(user);
        return true;
    }

    public async Task<bool> UpdateProfilePictureAsync(Guid userId, string imageUrl)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null) return false;

        user.ProfilePictureUrl = imageUrl;
        await _userRepository.UpdateAsync(user);
        return true;
    }
}
