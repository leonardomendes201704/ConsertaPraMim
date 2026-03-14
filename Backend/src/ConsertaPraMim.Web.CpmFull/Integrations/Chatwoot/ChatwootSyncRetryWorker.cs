using AppMobileCPM.Observability;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Chatwoot;

public sealed class ChatwootSyncRetryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ChatwootOptions _options;
    private readonly ILogger<ChatwootSyncRetryWorker> _logger;
    private readonly string _workerInstance;

    public ChatwootSyncRetryWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ChatwootOptions> options,
        ILogger<ChatwootSyncRetryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _workerInstance = $"{Environment.MachineName}-chatwoot-{Guid.NewGuid():N}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || !_options.RetryWorkerEnabled)
        {
            _logger.LogInformation("ChatwootSyncRetryWorker desabilitado por configuracao.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.RetryWorkerIntervalSeconds));
        _logger.LogInformation(
            "ChatwootSyncRetryWorker iniciado. Interval={IntervalSeconds}s BatchSize={BatchSize}.",
            interval.TotalSeconds,
            _options.RetryWorkerBatchSize);

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
                _logger.LogError(ex, "Erro inesperado ao processar fila de retentativa do Chatwoot.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var cycleScope = ChatwootCorrelationContext.Push(ChatwootCorrelationContext.Create("chatwoot-retry-cycle"));
        var cycleCorrelationId = ChatwootCorrelationContext.Current ?? ChatwootCorrelationContext.GetOrCreate("chatwoot-retry-cycle");
        using var scope = _scopeFactory.CreateScope();
        var queueService = scope.ServiceProvider.GetRequiredService<IChatwootSyncQueueService>();
        var leadSyncService = scope.ServiceProvider.GetRequiredService<IChatwootLeadSyncService>();
        var items = queueService.AcquireDueItems(
            _workerInstance,
            _options.RetryWorkerBatchSize,
            DateTime.UtcNow);

        if (items.Count == 0)
        {
            _logger.LogDebug("ChatwootSyncRetryWorker sem itens pendentes nesta execucao. CorrelationId={CorrelationId}", cycleCorrelationId);
            return 0;
        }

        var processed = 0;
        var retried = 0;
        var deadLetters = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var itemScope = ChatwootCorrelationContext.Push(ChatwootCorrelationContext.Create($"chatwoot-retry-{item.LeadId}"));
            var itemCorrelationId = ChatwootCorrelationContext.Current ?? ChatwootCorrelationContext.GetOrCreate($"chatwoot-retry-{item.LeadId}");
            _logger.LogInformation(
                "Processando item da fila Chatwoot. CorrelationId={CorrelationId} QueueItemId={QueueItemId} LeadId={LeadId} OperationType={OperationType} AttemptCount={AttemptCount}",
                itemCorrelationId,
                item.Id,
                item.LeadId,
                item.OperationType,
                item.AttemptCount);

            ChatwootLeadSyncResult result;
            switch (item.OperationType)
            {
                case ChatwootSyncOperationTypes.LeadSync:
                    result = await leadSyncService.SyncLeadAsync(item.LeadId, cancellationToken, queueOnFailure: false);
                    break;
                case ChatwootSyncOperationTypes.StageSync:
                    result = await leadSyncService.SyncLeadStageAsync(item.LeadId, cancellationToken, queueOnFailure: false);
                    break;
                default:
                    var unsupportedStatus = queueService.MarkFailed(
                        item,
                        _workerInstance,
                        $"Operacao de fila nao suportada: {item.OperationType}.",
                        retryRecommended: false);
                    if (unsupportedStatus == ChatwootSyncQueueStatuses.DeadLetter)
                    {
                        deadLetters++;
                    }

                    continue;
            }

            if (result.Succeeded)
            {
                queueService.MarkProcessed(item, _workerInstance, result.Message);
                processed++;
                continue;
            }

            var finalStatus = queueService.MarkFailed(item, _workerInstance, result.Message, result.RetrySuggested);
            if (finalStatus == ChatwootSyncQueueStatuses.Retrying)
            {
                retried++;
            }
            else if (finalStatus == ChatwootSyncQueueStatuses.DeadLetter)
            {
                deadLetters++;
            }
        }

        _logger.LogInformation(
            "ChatwootSyncRetryWorker processou {Total} itens. CorrelationId={CorrelationId} Success={Processed} RetryScheduled={Retried} DeadLetter={DeadLetters}.",
            items.Count,
            cycleCorrelationId,
            processed,
            retried,
            deadLetters);

        return items.Count;
    }
}
