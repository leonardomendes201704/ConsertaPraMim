using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.API.Services;

namespace ConsertaPraMim.API.BackgroundJobs;

public class ApiRequestTelemetryFlushWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRequestTelemetryBuffer _telemetryBuffer;
    private readonly ILogger<ApiRequestTelemetryFlushWorker> _logger;
    private readonly IAdminMonitoringRealtimeNotifier _realtimeNotifier;
    private readonly IMonitoringRuntimeSettings _monitoringRuntimeSettings;
    private readonly bool _enabled;
    private readonly int _batchSize;
    private readonly int _accumulateDelayMs;

    public ApiRequestTelemetryFlushWorker(
        IServiceScopeFactory scopeFactory,
        IRequestTelemetryBuffer telemetryBuffer,
        IAdminMonitoringRealtimeNotifier realtimeNotifier,
        IMonitoringRuntimeSettings monitoringRuntimeSettings,
        IConfiguration configuration,
        ILogger<ApiRequestTelemetryFlushWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _telemetryBuffer = telemetryBuffer;
        _realtimeNotifier = realtimeNotifier;
        _monitoringRuntimeSettings = monitoringRuntimeSettings;
        _logger = logger;

        var monitoringEnabled = ParseBool(configuration["Monitoring:Enabled"], defaultValue: true);
        var flushEnabled = ParseBool(configuration["Monitoring:FlushWorker:Enabled"], defaultValue: true);
        _enabled = monitoringEnabled && flushEnabled;
        _batchSize = Math.Clamp(ParseInt(configuration["Monitoring:FlushWorker:BatchSize"], 200), 10, 2000);
        _accumulateDelayMs = Math.Clamp(ParseInt(configuration["Monitoring:FlushWorker:AccumulateDelayMs"], 250), 10, 2000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("ApiRequestTelemetryFlushWorker disabled by configuration.");
            return;
        }

        _logger.LogInformation(
            "ApiRequestTelemetryFlushWorker started. BatchSize={BatchSize}, AccumulateDelayMs={AccumulateDelayMs}.",
            _batchSize,
            _accumulateDelayMs);

        var batch = new List<ApiRequestTelemetryEventDto>(_batchSize);
        while (!stoppingToken.IsCancellationRequested)
        {
            ApiRequestTelemetryEventDto firstEvent;
            try
            {
                firstEvent = await _telemetryBuffer.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            batch.Clear();
            batch.Add(firstEvent);

            try
            {
                await Task.Delay(_accumulateDelayMs, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // continue to flush what is buffered before leaving loop
            }

            while (batch.Count < _batchSize && _telemetryBuffer.TryDequeue(out var nextEvent) && nextEvent != null)
            {
                batch.Add(nextEvent);
            }

            if (!await _monitoringRuntimeSettings.IsTelemetryEnabledAsync(stoppingToken))
            {
                continue;
            }

            await PersistBatchAsync(batch, stoppingToken);
        }
    }

    private async Task PersistBatchAsync(
        IReadOnlyCollection<ApiRequestTelemetryEventDto> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var monitoringService = scope.ServiceProvider.GetRequiredService<IAdminMonitoringService>();
            await monitoringService.SaveRawEventsAsync(batch, cancellationToken);
            if (ShouldNotifyRealtime(batch))
            {
                await _realtimeNotifier.NotifyUpdatedAsync("raw-flush", batch.Count, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush telemetry batch. Dropping {BatchCount} events to protect API throughput.", batch.Count);
        }
    }

    private static bool ParseBool(string? raw, bool defaultValue)
    {
        return bool.TryParse(raw, out var parsed)
            ? parsed
            : defaultValue;
    }

    private static int ParseInt(string? raw, int defaultValue)
    {
        return int.TryParse(raw, out var parsed)
            ? parsed
            : defaultValue;
    }

    private static bool ShouldNotifyRealtime(IReadOnlyCollection<ApiRequestTelemetryEventDto> batch)
    {
        foreach (var item in batch)
        {
            if (item == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.Path))
            {
                return true;
            }

            if (!item.Path.StartsWith("/api/admin/monitoring", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
