using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.API.Services;

namespace ConsertaPraMim.API.BackgroundJobs;

public class ApiMonitoringAggregationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ApiMonitoringAggregationWorker> _logger;
    private readonly IAdminMonitoringRealtimeNotifier _realtimeNotifier;
    private readonly IMonitoringRuntimeSettings _monitoringRuntimeSettings;
    private readonly bool _enabled;
    private readonly TimeSpan _interval;
    private readonly AdminMonitoringMaintenanceOptionsDto _options;

    public ApiMonitoringAggregationWorker(
        IServiceScopeFactory scopeFactory,
        IAdminMonitoringRealtimeNotifier realtimeNotifier,
        IMonitoringRuntimeSettings monitoringRuntimeSettings,
        IConfiguration configuration,
        ILogger<ApiMonitoringAggregationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _realtimeNotifier = realtimeNotifier;
        _monitoringRuntimeSettings = monitoringRuntimeSettings;
        _logger = logger;

        var monitoringEnabled = ParseBool(configuration["Monitoring:Enabled"], defaultValue: true);
        var aggregationEnabled = ParseBool(configuration["Monitoring:AggregationWorker:Enabled"], defaultValue: true);
        _enabled = monitoringEnabled && aggregationEnabled;

        var intervalSeconds = Math.Clamp(ParseInt(configuration["Monitoring:AggregationWorker:IntervalSeconds"], 60), 15, 3600);
        _interval = TimeSpan.FromSeconds(intervalSeconds);

        _options = new AdminMonitoringMaintenanceOptionsDto(
            HourlyRecomputeWindowHours: Math.Clamp(ParseInt(configuration["Monitoring:AggregationWorker:HourlyRecomputeWindowHours"], 48), 1, 336),
            DailyRecomputeWindowDays: Math.Clamp(ParseInt(configuration["Monitoring:AggregationWorker:DailyRecomputeWindowDays"], 35), 1, 365),
            RawRetentionDays: Math.Clamp(ParseInt(configuration["Monitoring:Retention:RawDays"], 14), 1, 180),
            AggregateRetentionDays: Math.Clamp(ParseInt(configuration["Monitoring:Retention:AggregateDays"], 180), 7, 730));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("ApiMonitoringAggregationWorker disabled by configuration.");
            return;
        }

        _logger.LogInformation(
            "ApiMonitoringAggregationWorker started. Interval={IntervalSeconds}s HourlyWindow={HourlyWindow}h DailyWindow={DailyWindow}d.",
            _interval.TotalSeconds,
            _options.HourlyRecomputeWindowHours,
            _options.DailyRecomputeWindowDays);

        using var timer = new PeriodicTimer(_interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await _monitoringRuntimeSettings.IsTelemetryEnabledAsync(stoppingToken))
                {
                    await RunOnceAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected failure in ApiMonitoringAggregationWorker.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    public async Task<AdminMonitoringMaintenanceResultDto> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var monitoringService = scope.ServiceProvider.GetRequiredService<IAdminMonitoringService>();
        var result = await monitoringService.RebuildAggregatesAndRetentionAsync(_options, cancellationToken);

        _logger.LogDebug(
            "Monitoring aggregation completed. ProcessedRaw={ProcessedRaw} HourlyBuckets={HourlyBuckets} DailyBuckets={DailyBuckets} PurgedRaw={PurgedRaw}",
            result.ProcessedRawLogs,
            result.RecomputedHourlyBuckets,
            result.RecomputedDailyBuckets,
            result.PurgedRawLogs);

        var affectedCount = result.ProcessedRawLogs + result.RecomputedHourlyBuckets + result.RecomputedDailyBuckets;
        await _realtimeNotifier.NotifyUpdatedAsync("aggregation", affectedCount, cancellationToken);

        return result;
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
}
