using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface ILandingTelemetryEventService
{
    Task<RecordLandingTelemetryBatchResponseDto> RecordBatchAsync(
        RecordLandingTelemetryBatchRequestDto request,
        LandingLeadCaptureContextDto context,
        CancellationToken cancellationToken = default);
}
