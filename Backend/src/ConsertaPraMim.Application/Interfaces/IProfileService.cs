using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IProfileService
{
    Task<UserProfileDto?> GetProfileAsync(Guid userId);
    Task<bool> UpdateProviderProfileAsync(Guid userId, UpdateProviderProfileDto dto);
}
