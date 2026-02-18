using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IRequestTelemetryBuffer
{
    bool TryEnqueue(ApiRequestTelemetryEventDto telemetryEvent);
    int ApproximateQueueLength { get; }
    ValueTask<ApiRequestTelemetryEventDto> DequeueAsync(CancellationToken cancellationToken = default);
    bool TryDequeue(out ApiRequestTelemetryEventDto? telemetryEvent);
}
