using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyStageAutomationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JourneyStageAutomationOptions _options;
    private readonly ILogger<JourneyStageAutomationWorker> _logger;

    public JourneyStageAutomationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<JourneyStageAutomationOptions> options,
        ILogger<JourneyStageAutomationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || !_options.WorkerEnabled)
        {
            _logger.LogInformation("JourneyStageAutomationWorker desabilitado por configuracao.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.WorkerIntervalSeconds));
        _logger.LogInformation(
            "JourneyStageAutomationWorker iniciado. Interval={IntervalSeconds}s BatchSize={BatchSize}.",
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
                _logger.LogError(ex, "Erro inesperado ao automatizar etapas do Kanban da jornada.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    public async Task<JourneyStageAutomationRunResult> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IJourneyStageAutomationService>();
        return await service.RunOnceAsync(DateTime.UtcNow, cancellationToken);
    }
}
