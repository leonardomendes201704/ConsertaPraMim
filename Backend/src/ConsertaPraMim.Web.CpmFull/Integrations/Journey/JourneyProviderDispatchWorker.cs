using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyProviderDispatchWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JourneyProviderDispatchOptions _options;
    private readonly ILogger<JourneyProviderDispatchWorker> _logger;

    public JourneyProviderDispatchWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<JourneyProviderDispatchOptions> options,
        ILogger<JourneyProviderDispatchWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || !_options.WorkerEnabled)
        {
            _logger.LogInformation("JourneyProviderDispatchWorker desabilitado por configuracao.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.WorkerIntervalSeconds));
        _logger.LogInformation(
            "JourneyProviderDispatchWorker iniciado. Interval={IntervalSeconds}s BatchSize={BatchSize}.",
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
                _logger.LogError(ex, "Erro inesperado ao executar o motor de disparo em ondas.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    public async Task<JourneyProviderDispatchRunResult> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IJourneyProviderDispatchService>();
        return await service.RunOnceAsync(DateTime.UtcNow, cancellationToken);
    }
}
