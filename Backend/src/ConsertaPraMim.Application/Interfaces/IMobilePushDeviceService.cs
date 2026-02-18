using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IMobilePushDeviceService
{
    Task<MobilePushDeviceRegistrationResponseDto> RegisterAsync(
        Guid userId,
        string appKind,
        MobilePushDeviceRegisterRequestDto request,
        CancellationToken cancellationToken = default);

    Task<MobilePushDeviceUnregisterResponseDto> UnregisterAsync(
        Guid userId,
        string appKind,
        MobilePushDeviceUnregisterRequestDto request,
        CancellationToken cancellationToken = default);
}
