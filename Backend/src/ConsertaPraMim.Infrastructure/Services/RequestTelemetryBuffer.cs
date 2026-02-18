using System.Threading.Channels;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ConsertaPraMim.Infrastructure.Services;

public class RequestTelemetryBuffer : IRequestTelemetryBuffer
{
    private readonly Channel<ApiRequestTelemetryEventDto> _channel;
    private int _queueLength;

    public RequestTelemetryBuffer(IConfiguration configuration)
    {
        var capacity = ParseInt(
            configuration["Monitoring:TelemetryBuffer:Capacity"],
            defaultValue: 20000,
            min: 1000,
            max: 200000);

        var options = new BoundedChannelOptions(capacity)
        {
            SingleReader = false,
            SingleWriter = false,
            // Com TryWrite, modo Wait retorna false ao atingir capacidade;
            // o caller decide descartar sem bloquear o pipeline HTTP.
            FullMode = BoundedChannelFullMode.Wait
        };

        _channel = Channel.CreateBounded<ApiRequestTelemetryEventDto>(options);
    }

    public int ApproximateQueueLength => Math.Max(0, Volatile.Read(ref _queueLength));

    public bool TryEnqueue(ApiRequestTelemetryEventDto telemetryEvent)
    {
        var written = _channel.Writer.TryWrite(telemetryEvent);
        if (written)
        {
            Interlocked.Increment(ref _queueLength);
        }

        return written;
    }

    public ValueTask<ApiRequestTelemetryEventDto> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return ReadAndTrackAsync(cancellationToken);
    }

    public bool TryDequeue(out ApiRequestTelemetryEventDto? telemetryEvent)
    {
        var read = _channel.Reader.TryRead(out var item);
        telemetryEvent = item;
        if (read)
        {
            Interlocked.Decrement(ref _queueLength);
        }

        return read;
    }

    private static int ParseInt(string? raw, int defaultValue, int min, int max)
    {
        if (!int.TryParse(raw, out var parsed))
        {
            return defaultValue;
        }

        return Math.Clamp(parsed, min, max);
    }

    private async ValueTask<ApiRequestTelemetryEventDto> ReadAndTrackAsync(CancellationToken cancellationToken)
    {
        var item = await _channel.Reader.ReadAsync(cancellationToken);
        Interlocked.Decrement(ref _queueLength);
        return item;
    }
}
