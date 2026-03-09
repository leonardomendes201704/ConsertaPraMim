using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface ILandingLeadService
{
    Task<CaptureLandingLeadResponseDto> CaptureAsync(
        CaptureLandingLeadRequestDto request,
        LandingLeadCaptureContextDto context,
        CancellationToken cancellationToken = default);
}
