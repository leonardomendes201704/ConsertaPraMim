using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyProviderMatchingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JourneyProviderMatchingOptions _options;
    private readonly ILogger<JourneyProviderMatchingWorker> _logger;

    public JourneyProviderMatchingWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<JourneyProviderMatchingOptions> options,
        ILogger<JourneyProviderMatchingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || !_options.WorkerEnabled)
        {
            _logger.LogInformation("JourneyProviderMatchingWorker desabilitado por configuracao.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.WorkerIntervalSeconds));
        _logger.LogInformation(
            "JourneyProviderMatchingWorker iniciado. Interval={IntervalSeconds}s BatchSize={BatchSize}.",
            interval.TotalSeconds,
            _options.WorkerBatchSize);

        using var timer = new PeriodicTimer(interval);
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
                _logger.LogError(ex, "Erro inesperado ao executar o matching geografico da jornada.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    public async Task<JourneyProviderMatchingRunResult> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IJourneyProviderMatchingService>();
        return await service.RunOnceAsync(DateTime.UtcNow, cancellationToken);
    }
}
