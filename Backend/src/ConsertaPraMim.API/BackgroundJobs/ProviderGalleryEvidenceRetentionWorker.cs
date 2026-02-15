using ConsertaPraMim.Application.Interfaces;

namespace ConsertaPraMim.API.BackgroundJobs;

public class ProviderGalleryEvidenceRetentionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProviderGalleryEvidenceRetentionWorker> _logger;
    private readonly bool _enabled;
    private readonly TimeSpan _interval;
    private readonly int _retentionDays;
    private readonly int _batchSize;

    public ProviderGalleryEvidenceRetentionWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ProviderGalleryEvidenceRetentionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        _enabled = ParseBoolean(configuration["ProviderGallery:EvidenceRetention:EnableWorker"], defaultValue: true);
        _interval = TimeSpan.FromMinutes(ParseInt(configuration["ProviderGallery:EvidenceRetention:WorkerIntervalMinutes"], 360, 5, 1440));
        _retentionDays = ParseInt(configuration["ProviderGallery:EvidenceRetention:RetentionDays"], 180, 1, 3650);
        _batchSize = ParseInt(configuration["ProviderGallery:EvidenceRetention:BatchSize"], 200, 1, 2000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("ProviderGalleryEvidenceRetentionWorker disabled by configuration.");
            return;
        }

        _logger.LogInformation(
            "ProviderGalleryEvidenceRetentionWorker started. Interval={IntervalMinutes}m RetentionDays={RetentionDays} BatchSize={BatchSize}.",
            _interval.TotalMinutes,
            _retentionDays,
            _batchSize);

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
                _logger.LogError(ex, "Unexpected error processing evidence retention cleanup.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var galleryService = scope.ServiceProvider.GetRequiredService<IProviderGalleryService>();

        var result = await galleryService.CleanupOldOperationalEvidencesAsync(
            _retentionDays,
            _batchSize,
            cancellationToken);

        if (result.DeletedCount > 0)
        {
            _logger.LogInformation(
                "Evidence retention cleanup removed {DeletedCount} item(s) after scanning {ScannedCount} candidate(s). OlderThanUtc={OlderThanUtc:o}.",
                result.DeletedCount,
                result.ScannedCount,
                result.OlderThanUtc);
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
