using AppMobileCPM.Observability;
using AppMobileCPM.Services;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Chatwoot;

public sealed class ChatwootSyncQueueService : IChatwootSyncQueueService
{
    private readonly IAdminKanbanService _kanbanService;
    private readonly ChatwootOptions _options;
    private readonly ILogger<ChatwootSyncQueueService> _logger;

    public ChatwootSyncQueueService(
        IAdminKanbanService kanbanService,
        IOptions<ChatwootOptions> options,
        ILogger<ChatwootSyncQueueService> logger)
    {
        _kanbanService = kanbanService;
        _options = options.Value;
        _logger = logger;
    }

    public void EnqueueRetry(int leadId, string operationType, string reason, bool runImmediately = false)
    {
        var correlationId = ChatwootCorrelationContext.GetOrCreate($"chatwoot-queue-{leadId}");
        var normalizedOperationType = NormalizeOperationType(operationType);
        var utcNow = DateTime.UtcNow;
        var queueItem = _kanbanService.EnqueueChatwootSyncQueueItem(new AdminKanbanChatwootSyncQueueEnqueueRequest
        {
            LeadId = leadId,
            OperationType = normalizedOperationType,
            NextAttemptAt = runImmediately ? utcNow : utcNow.Add(ResolveRetryDelay(0)),
            MaxAttempts = _options.SyncQueueMaxAttempts,
            LastError = ChatwootSecuritySanitizer.SanitizeMessage(reason, 1000)
        });

        var description = runImmediately
            ? $"Retentativa manual do Chatwoot enfileirada para processamento imediato em {FormatOperationLabel(normalizedOperationType)}."
            : $"Retentativa automatica do Chatwoot enfileirada para {FormatOperationLabel(normalizedOperationType)} apos falha externa.";

        _ = _kanbanService.AddHistoryEvent(leadId, "chatwoot_retentativa_enfileirada", description);
        _logger.LogInformation(
            "Fila Chatwoot | CorrelationId={CorrelationId} LeadId={LeadId} OperationType={OperationType} QueueItemId={QueueItemId} RunImmediately={RunImmediately}",
            correlationId,
            leadId,
            normalizedOperationType,
            queueItem.Id,
            runImmediately);
    }

    public IReadOnlyList<AdminKanbanChatwootSyncQueueItemRecord> AcquireDueItems(string workerInstance, int batchSize, DateTime utcNow)
    {
        return _kanbanService.AcquireDueChatwootSyncQueueItems(batchSize, utcNow, workerInstance);
    }

    public string MarkProcessed(AdminKanbanChatwootSyncQueueItemRecord item, string workerInstance, string? note = null)
    {
        var correlationId = ChatwootCorrelationContext.GetOrCreate($"chatwoot-queue-{item.LeadId}");
        var status = ChatwootSyncQueueStatuses.Processed;
        _ = _kanbanService.FinalizeChatwootSyncQueueItem(new AdminKanbanChatwootSyncQueueFinalizeRequest
        {
            QueueItemId = item.Id,
            FinalStatus = status,
            FinalizedAt = DateTime.UtcNow,
            ClearLastError = true,
            WorkerInstance = workerInstance
        });

        _ = _kanbanService.AddHistoryEvent(
            item.LeadId,
            "chatwoot_retentativa_processada",
            $"Retentativa do Chatwoot concluida com sucesso em {FormatOperationLabel(item.OperationType)}.");
        _logger.LogInformation(
            "Fila Chatwoot processada com sucesso. CorrelationId={CorrelationId} LeadId={LeadId} QueueItemId={QueueItemId} OperationType={OperationType} WorkerInstance={WorkerInstance}",
            correlationId,
            item.LeadId,
            item.Id,
            item.OperationType,
            workerInstance);

        return status;
    }

    public string MarkFailed(AdminKanbanChatwootSyncQueueItemRecord item, string workerInstance, string errorMessage, bool retryRecommended)
    {
        var correlationId = ChatwootCorrelationContext.GetOrCreate($"chatwoot-queue-{item.LeadId}");
        var utcNow = DateTime.UtcNow;
        var sanitizedError = ChatwootSecuritySanitizer.SanitizeMessage(errorMessage, 1000);

        if (!retryRecommended || item.AttemptCount >= item.MaxAttempts)
        {
            _ = _kanbanService.FinalizeChatwootSyncQueueItem(new AdminKanbanChatwootSyncQueueFinalizeRequest
            {
                QueueItemId = item.Id,
                FinalStatus = ChatwootSyncQueueStatuses.DeadLetter,
                FinalizedAt = utcNow,
                LastError = sanitizedError,
                WorkerInstance = workerInstance
            });

            _ = _kanbanService.AddHistoryEvent(
                item.LeadId,
                "chatwoot_dead_letter",
                $"Retentativa do Chatwoot esgotou o limite em {FormatOperationLabel(item.OperationType)}. Ultimo erro: {sanitizedError}");
            _logger.LogWarning(
                "Fila Chatwoot movida para dead-letter. CorrelationId={CorrelationId} LeadId={LeadId} QueueItemId={QueueItemId} OperationType={OperationType} AttemptCount={AttemptCount} MaxAttempts={MaxAttempts}",
                correlationId,
                item.LeadId,
                item.Id,
                item.OperationType,
                item.AttemptCount,
                item.MaxAttempts);

            return ChatwootSyncQueueStatuses.DeadLetter;
        }

        var nextAttemptAt = utcNow.Add(ResolveRetryDelay(item.AttemptCount));
        _ = _kanbanService.FinalizeChatwootSyncQueueItem(new AdminKanbanChatwootSyncQueueFinalizeRequest
        {
            QueueItemId = item.Id,
            FinalStatus = ChatwootSyncQueueStatuses.Retrying,
            FinalizedAt = utcNow,
            NextAttemptAt = nextAttemptAt,
            LastError = sanitizedError,
            WorkerInstance = workerInstance
        });

        _ = _kanbanService.AddHistoryEvent(
            item.LeadId,
            "chatwoot_retentativa_enfileirada",
            $"Nova retentativa do Chatwoot foi reagendada para {FormatOperationLabel(item.OperationType)} apos falha transiente.");
        _logger.LogWarning(
            "Fila Chatwoot reagendada. CorrelationId={CorrelationId} LeadId={LeadId} QueueItemId={QueueItemId} OperationType={OperationType} AttemptCount={AttemptCount} NextAttemptAt={NextAttemptAt}",
            correlationId,
            item.LeadId,
            item.Id,
            item.OperationType,
            item.AttemptCount,
            nextAttemptAt);

        return ChatwootSyncQueueStatuses.Retrying;
    }

    public int CompleteActiveRetriesForLead(int leadId, IReadOnlyCollection<string> operationTypes, string? note = null)
    {
        if (operationTypes.Count == 0)
        {
            return 0;
        }

        var utcNow = DateTime.UtcNow;
        var completed = 0;
        foreach (var operationType in operationTypes
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Select(NormalizeOperationType)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            completed += _kanbanService.CompleteActiveChatwootSyncQueueItems(
                leadId,
                operationType,
                ChatwootSyncQueueStatuses.Processed,
                lastError: null,
                completedAtUtc: utcNow);
        }

        return completed;
    }

    public string ResolveOperationType(AdminKanbanLeadDetailsRecord lead)
    {
        ArgumentNullException.ThrowIfNull(lead);

        return lead.Chatwoot.ConversationId.HasValue
            ? ChatwootSyncOperationTypes.StageSync
            : ChatwootSyncOperationTypes.LeadSync;
    }

    private static string NormalizeOperationType(string operationType)
    {
        var normalized = (operationType ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            ChatwootSyncOperationTypes.LeadSync => ChatwootSyncOperationTypes.LeadSync,
            ChatwootSyncOperationTypes.StageSync => ChatwootSyncOperationTypes.StageSync,
            _ => throw new InvalidOperationException($"Operacao Chatwoot nao suportada para fila: '{operationType}'.")
        };
    }

    private static TimeSpan ResolveRetryDelay(int attemptCount)
    {
        return attemptCount switch
        {
            <= 0 => TimeSpan.FromMinutes(1),
            1 => TimeSpan.FromMinutes(5),
            2 => TimeSpan.FromMinutes(15),
            3 => TimeSpan.FromHours(1),
            _ => TimeSpan.FromHours(6)
        };
    }

    private static string FormatOperationLabel(string operationType)
    {
        return NormalizeOperationType(operationType) switch
        {
            ChatwootSyncOperationTypes.LeadSync => "sincronizacao do lead",
            ChatwootSyncOperationTypes.StageSync => "sincronizacao da etapa",
            _ => "sincronizacao do Chatwoot"
        };
    }

    private static string TrimTo(string? value, int maxLength)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}
