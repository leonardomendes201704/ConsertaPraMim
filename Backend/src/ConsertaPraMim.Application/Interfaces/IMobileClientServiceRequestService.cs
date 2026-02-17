using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IMobileClientServiceRequestService
{
    Task<IReadOnlyList<MobileClientServiceRequestCategoryDto>> GetActiveCategoriesAsync();
    Task<MobileClientResolveZipResponseDto?> ResolveZipAsync(string zipCode);
    Task<MobileClientCreateServiceRequestResponseDto> CreateAsync(
        Guid clientUserId,
        MobileClientCreateServiceRequestRequestDto request);
}
