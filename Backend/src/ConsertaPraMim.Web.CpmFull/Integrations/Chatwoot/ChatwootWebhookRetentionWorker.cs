using AppMobileCPM.Observability;
using AppMobileCPM.Services;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Chatwoot;

public sealed class ChatwootWebhookRetentionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ChatwootOptions _options;
    private readonly ILogger<ChatwootWebhookRetentionWorker> _logger;

    public ChatwootWebhookRetentionWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ChatwootOptions> options,
        ILogger<ChatwootWebhookRetentionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.WebhookPayloadCleanupEnabled)
        {
            _logger.LogInformation("ChatwootWebhookRetentionWorker desabilitado por configuracao.");
            return;
        }

        var interval = _options.GetWebhookPayloadCleanupInterval();
        _logger.LogInformation(
            "ChatwootWebhookRetentionWorker iniciado. IntervalMinutes={IntervalMinutes} RetentionDays={RetentionDays}.",
            interval.TotalMinutes,
            _options.WebhookPayloadRetentionDays);

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
                _logger.LogError(ex, "Erro inesperado na limpeza de payloads do webhook do Chatwoot.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    public Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var scope = _scopeFactory.CreateScope();
        using var correlationScope = ChatwootCorrelationContext.Push(ChatwootCorrelationContext.Create("chatwoot-webhook-retention"));
        var correlationId = ChatwootCorrelationContext.Current ?? ChatwootCorrelationContext.GetOrCreate("chatwoot-webhook-retention");
        var kanbanService = scope.ServiceProvider.GetRequiredService<IAdminKanbanService>();
        var utcNow = DateTime.UtcNow;
        var purgeBeforeUtc = utcNow.AddDays(-Math.Max(1, _options.WebhookPayloadRetentionDays));
        var affectedRows = kanbanService.PurgeChatwootWebhookPayloads(purgeBeforeUtc, utcNow);

        if (affectedRows > 0)
        {
            _logger.LogInformation(
                "Retention do webhook Chatwoot executada. CorrelationId={CorrelationId} PurgedRows={PurgedRows} PurgeBeforeUtc={PurgeBeforeUtc}",
                correlationId,
                affectedRows,
                purgeBeforeUtc);
        }
        else
        {
            _logger.LogDebug(
                "Retention do webhook Chatwoot sem payloads elegiveis. CorrelationId={CorrelationId} PurgeBeforeUtc={PurgeBeforeUtc}",
                correlationId,
                purgeBeforeUtc);
        }

        return Task.FromResult(affectedRows);
    }
}
