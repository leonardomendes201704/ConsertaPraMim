using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Telegram;

public sealed class TelegramDeliveryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TelegramAutomationOptions _options;
    private readonly ILogger<TelegramDeliveryWorker> _logger;
    private readonly string _workerInstance;

    public TelegramDeliveryWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<TelegramAutomationOptions> options,
        ILogger<TelegramDeliveryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _workerInstance = $"{Environment.MachineName}-telegram-{Guid.NewGuid():N}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || !_options.MirrorMessagesEnabled || !_options.DeliveryWorkerEnabled)
        {
            _logger.LogInformation("TelegramDeliveryWorker desabilitado por configuracao.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.DeliveryWorkerIntervalSeconds));
        _logger.LogInformation(
            "TelegramDeliveryWorker iniciado. Interval={IntervalSeconds}s BatchSize={BatchSize}.",
            interval.TotalSeconds,
            _options.DeliveryWorkerBatchSize);

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
                _logger.LogError(ex, "Erro inesperado ao processar fila bidirecional do Telegram.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var queueService = scope.ServiceProvider.GetRequiredService<ITelegramDeliveryQueueService>();
        var automationService = scope.ServiceProvider.GetRequiredService<ITelegramMessageAutomationService>();
        var items = queueService.AcquireDueItems(_workerInstance, _options.DeliveryWorkerBatchSize, DateTime.UtcNow);
        if (items.Count == 0)
        {
            return 0;
        }

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await automationService.ProcessQueueItemAsync(item, cancellationToken);
            if (result.Succeeded)
            {
                queueService.MarkProcessed(item, _workerInstance, result.Message);
                continue;
            }

            queueService.MarkFailed(item, _workerInstance, result.Message, result.RetrySuggested);
        }

        return items.Count;
    }
}
