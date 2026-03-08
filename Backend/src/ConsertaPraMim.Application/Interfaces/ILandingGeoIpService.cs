using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface ILandingGeoIpService
{
    Task<LandingGeoIpLookupResultDto> LookupAsync(
        string? ipAddress,
        string? forwardedFor,
        CancellationToken cancellationToken = default);
}
