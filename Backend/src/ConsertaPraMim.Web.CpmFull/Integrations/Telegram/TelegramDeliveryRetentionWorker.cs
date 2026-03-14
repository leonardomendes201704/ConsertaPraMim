using AppMobileCPM.Observability;
using AppMobileCPM.Services;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Telegram;

public sealed class TelegramDeliveryRetentionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TelegramAutomationOptions _options;
    private readonly ILogger<TelegramDeliveryRetentionWorker> _logger;

    public TelegramDeliveryRetentionWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<TelegramAutomationOptions> options,
        ILogger<TelegramDeliveryRetentionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.DeliveryPayloadCleanupEnabled)
        {
            _logger.LogInformation("TelegramDeliveryRetentionWorker desabilitado por configuracao.");
            return;
        }

        var interval = _options.GetDeliveryPayloadCleanupInterval();
        _logger.LogInformation(
            "TelegramDeliveryRetentionWorker iniciado. IntervalMinutes={IntervalMinutes} RetentionDays={RetentionDays}.",
            interval.TotalMinutes,
            _options.DeliveryPayloadRetentionDays);

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
                _logger.LogError(ex, "Erro inesperado na limpeza de payloads da fila Telegram.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    public Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var scope = _scopeFactory.CreateScope();
        using var correlationScope = ChatwootCorrelationContext.Push(ChatwootCorrelationContext.Create("telegram-delivery-retention"));
        var correlationId = ChatwootCorrelationContext.Current ?? ChatwootCorrelationContext.GetOrCreate("telegram-delivery-retention");
        var kanbanService = scope.ServiceProvider.GetRequiredService<IAdminKanbanService>();
        var utcNow = DateTime.UtcNow;
        var purgeBeforeUtc = utcNow.AddDays(-Math.Max(1, _options.DeliveryPayloadRetentionDays));
        var affectedRows = kanbanService.PurgeTelegramDeliveryPayloads(purgeBeforeUtc, utcNow);

        if (affectedRows > 0)
        {
            _logger.LogInformation(
                "Retention da fila Telegram executada. CorrelationId={CorrelationId} PurgedRows={PurgedRows} PurgeBeforeUtc={PurgeBeforeUtc}",
                correlationId,
                affectedRows,
                purgeBeforeUtc);
        }
        else
        {
            _logger.LogDebug(
                "Retention da fila Telegram sem payloads elegiveis. CorrelationId={CorrelationId} PurgeBeforeUtc={PurgeBeforeUtc}",
                correlationId,
                purgeBeforeUtc);
        }

        return Task.FromResult(affectedRows);
    }
}
