using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.API.BackgroundJobs;

public class MobilePushDevicesCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MobilePushDevicesCleanupWorker> _logger;
    private readonly bool _enabled;
    private readonly TimeSpan _interval;
    private readonly int _staleDays;
    private readonly bool _hardDeleteInactive;
    private readonly int _deleteAfterDays;

    public MobilePushDevicesCleanupWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<MobilePushDevicesCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        _enabled = ParseBoolean(configuration["PushNotifications:DeviceCleanup:Enabled"], defaultValue: true);
        _interval = TimeSpan.FromMinutes(ParseInt(configuration["PushNotifications:DeviceCleanup:IntervalMinutes"], 1440, 10, 10080));
        _staleDays = ParseInt(configuration["PushNotifications:DeviceCleanup:DeactivateAfterDaysWithoutSeen"], 90, 1, 3650);
        _hardDeleteInactive = ParseBoolean(configuration["PushNotifications:DeviceCleanup:HardDeleteInactive"], defaultValue: false);
        _deleteAfterDays = ParseInt(configuration["PushNotifications:DeviceCleanup:DeleteInactiveAfterDays"], 180, 1, 3650);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("MobilePushDevicesCleanupWorker disabled by configuration.");
            return;
        }

        _logger.LogInformation(
            "MobilePushDevicesCleanupWorker started. Interval={IntervalMinutes}m StaleDays={StaleDays} HardDeleteInactive={HardDeleteInactive} DeleteAfterDays={DeleteAfterDays}.",
            _interval.TotalMinutes,
            _staleDays,
            _hardDeleteInactive,
            _deleteAfterDays);

        using var timer = new PeriodicTimer(_interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in mobile push cleanup worker.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMobilePushDeviceRepository>();

        var staleBeforeUtc = DateTime.UtcNow.AddDays(-_staleDays);
        var deactivatedCount = await repository.DeactivateStaleActiveAsync(
            staleBeforeUtc,
            "stale_last_seen_cleanup",
            cancellationToken);

        var deletedCount = 0;
        if (_hardDeleteInactive)
        {
            var deleteBeforeUtc = DateTime.UtcNow.AddDays(-_deleteAfterDays);
            deletedCount = await repository.DeleteInactiveOlderThanAsync(deleteBeforeUtc, cancellationToken);
        }

        if (deactivatedCount > 0 || deletedCount > 0)
        {
            _logger.LogInformation(
                "Mobile push cleanup executed. Deactivated={DeactivatedCount} Deleted={DeletedCount} StaleBeforeUtc={StaleBeforeUtc:o}.",
                deactivatedCount,
                deletedCount,
                staleBeforeUtc);
        }
    }

    private static int ParseInt(string? raw, int defaultValue, int min, int max)
    {
        if (!int.TryParse(raw, out var parsed))
        {
            return defaultValue;
        }

        return Math.Clamp(parsed, min, max);
    }

    private static bool ParseBoolean(string? raw, bool defaultValue)
    {
        if (!bool.TryParse(raw, out var parsed))
        {
            return defaultValue;
        }

        return parsed;
    }
}
