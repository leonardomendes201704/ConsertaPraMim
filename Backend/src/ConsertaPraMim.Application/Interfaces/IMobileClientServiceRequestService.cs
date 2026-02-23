using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IMobileClientServiceRequestService
{
    Task<IReadOnlyList<MobileClientServiceRequestCategoryDto>> GetActiveCategoriesAsync();
    Task<MobileClientResolveZipResponseDto?> ResolveZipAsync(string zipCode);
    Task<MobileClientResolveZipResponseDto?> ResolveCurrentLocationAsync(double latitude, double longitude);
    Task<MobileClientCreateServiceRequestResponseDto> CreateAsync(
        Guid clientUserId,
        MobileClientCreateServiceRequestRequestDto request);
}
