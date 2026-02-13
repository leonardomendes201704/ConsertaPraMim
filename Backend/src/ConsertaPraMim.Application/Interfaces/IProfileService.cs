using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.Interfaces;

public interface IProfileService
{
    Task<UserProfileDto?> GetProfileAsync(Guid userId);
    Task<bool> UpdateProviderProfileAsync(Guid userId, UpdateProviderProfileDto dto);
    Task<bool> UpdateProviderOperationalStatusAsync(Guid userId, ProviderOperationalStatus status);
    Task<ProviderOperationalStatus?> GetProviderOperationalStatusAsync(Guid userId);
    Task<bool> UpdateProfilePictureAsync(Guid userId, string imageUrl);
}
