using ConsertaPraMim.Application.Interfaces;

namespace ConsertaPraMim.API.BackgroundJobs;

public sealed class GoogleCalendarSyncRetryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GoogleCalendarSyncRetryWorker> _logger;
    private readonly bool _enabled;
    private readonly TimeSpan _interval;
    private readonly int _batchSize;

    public GoogleCalendarSyncRetryWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<GoogleCalendarSyncRetryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _enabled = ParseBoolean(configuration["GoogleCalendarSync:RetryWorkerEnabled"], defaultValue: true);
        _interval = TimeSpan.FromSeconds(ParseInt(configuration["GoogleCalendarSync:RetryWorkerIntervalSeconds"], 30, 5, 3600));
        _batchSize = ParseInt(configuration["GoogleCalendarSync:RetryWorkerBatchSize"], 100, 1, 1000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("GoogleCalendarSyncRetryWorker desabilitado por configuracao.");
            return;
        }

        _logger.LogInformation(
            "GoogleCalendarSyncRetryWorker iniciado. Interval={IntervalSeconds}s BatchSize={BatchSize}.",
            _interval.TotalSeconds,
            _batchSize);

        using var timer = new PeriodicTimer(_interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao processar retry do Google Calendar sync.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IGoogleCalendarSyncOperationsService>();
        var processed = await service.ProcessDueRetriesAsync(_batchSize, cancellationToken);
        if (processed > 0)
        {
            _logger.LogInformation("GoogleCalendarSyncRetryWorker processou {ProcessedCount} itens.", processed);
        }

        return processed;
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
