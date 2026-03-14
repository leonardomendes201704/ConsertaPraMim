using AppMobileCPM.Integrations.Chatwoot;
using AppMobileCPM.Observability;
using AppMobileCPM.Services;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Telegram;

public sealed class TelegramDeliveryQueueService : ITelegramDeliveryQueueService
{
    private readonly IAdminKanbanService _kanbanService;
    private readonly TelegramAutomationOptions _options;
    private readonly ILogger<TelegramDeliveryQueueService> _logger;

    public TelegramDeliveryQueueService(
        IAdminKanbanService kanbanService,
        IOptions<TelegramAutomationOptions> options,
        ILogger<TelegramDeliveryQueueService> logger)
    {
        _kanbanService = kanbanService;
        _options = options.Value;
        _logger = logger;
    }

    public AdminKanbanTelegramDeliveryQueueItemRecord Enqueue(
        int leadId,
        string direction,
        string deliveryKey,
        string payloadJson,
        long? chatwootConversationId,
        long? telegramChatId,
        string reason,
        bool runImmediately = false)
    {
        var utcNow = DateTime.UtcNow;
        var normalizedDirection = NormalizeDirection(direction);
        var queueItem = _kanbanService.EnqueueTelegramDeliveryQueueItem(new AdminKanbanTelegramDeliveryQueueEnqueueRequest
        {
            LeadId = leadId,
            Direction = normalizedDirection,
            DeliveryKey = deliveryKey,
            PayloadJson = payloadJson,
            ChatwootConversationId = chatwootConversationId,
            TelegramChatId = telegramChatId,
            NextAttemptAt = runImmediately ? utcNow : utcNow.Add(ResolveRetryDelay(0)),
            MaxAttempts = _options.DeliveryQueueMaxAttempts,
            LastError = reason
        });

        if (!queueItem.IsDuplicate)
        {
            _ = _kanbanService.AddHistoryEvent(
                leadId,
                "telegram_entrega_enfileirada",
                $"Entrega {FormatDirectionLabel(normalizedDirection)} enfileirada para processamento.");
        }

        _logger.LogInformation(
            "Fila Telegram | LeadId={LeadId} Direction={Direction} QueueItemId={QueueItemId} Duplicate={Duplicate}",
            leadId,
            normalizedDirection,
            queueItem.Id,
            queueItem.IsDuplicate);

        return queueItem;
    }

    public IReadOnlyList<AdminKanbanTelegramDeliveryQueueItemRecord> AcquireDueItems(string workerInstance, int batchSize, DateTime utcNow)
    {
        return _kanbanService.AcquireDueTelegramDeliveryQueueItems(batchSize, utcNow, workerInstance);
    }

    public string MarkProcessed(AdminKanbanTelegramDeliveryQueueItemRecord item, string workerInstance, string? note = null)
    {
        _ = _kanbanService.FinalizeTelegramDeliveryQueueItem(new AdminKanbanTelegramDeliveryQueueFinalizeRequest
        {
            QueueItemId = item.Id,
            FinalStatus = TelegramDeliveryQueueStatuses.Processed,
            FinalizedAt = DateTime.UtcNow,
            ClearLastError = true,
            WorkerInstance = workerInstance
        });

        _logger.LogInformation(
            "Fila Telegram processada com sucesso. LeadId={LeadId} Direction={Direction} QueueItemId={QueueItemId}",
            item.LeadId,
            item.Direction,
            item.Id);

        return TelegramDeliveryQueueStatuses.Processed;
    }

    public string MarkFailed(AdminKanbanTelegramDeliveryQueueItemRecord item, string workerInstance, string errorMessage, bool retryRecommended)
    {
        var sanitizedError = TelegramSecuritySanitizer.SanitizeMessage(errorMessage, 1000);
        var utcNow = DateTime.UtcNow;

        if (!retryRecommended || item.AttemptCount >= item.MaxAttempts)
        {
            _ = _kanbanService.FinalizeTelegramDeliveryQueueItem(new AdminKanbanTelegramDeliveryQueueFinalizeRequest
            {
                QueueItemId = item.Id,
                FinalStatus = TelegramDeliveryQueueStatuses.DeadLetter,
                FinalizedAt = utcNow,
                LastError = sanitizedError,
                WorkerInstance = workerInstance
            });

            _ = _kanbanService.AddHistoryEvent(
                item.LeadId,
                "telegram_dead_letter",
                $"Entrega {FormatDirectionLabel(item.Direction)} esgotou o limite de tentativas. Ultimo erro: {sanitizedError}");

            return TelegramDeliveryQueueStatuses.DeadLetter;
        }

        _ = _kanbanService.FinalizeTelegramDeliveryQueueItem(new AdminKanbanTelegramDeliveryQueueFinalizeRequest
        {
            QueueItemId = item.Id,
            FinalStatus = TelegramDeliveryQueueStatuses.Retrying,
            FinalizedAt = utcNow,
            NextAttemptAt = utcNow.Add(ResolveRetryDelay(item.AttemptCount)),
            LastError = sanitizedError,
            WorkerInstance = workerInstance
        });

        return TelegramDeliveryQueueStatuses.Retrying;
    }

    private static string NormalizeDirection(string direction)
    {
        var normalized = (direction ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            TelegramDeliveryDirections.TelegramToChatwoot => TelegramDeliveryDirections.TelegramToChatwoot,
            TelegramDeliveryDirections.ChatwootToTelegram => TelegramDeliveryDirections.ChatwootToTelegram,
            _ => throw new InvalidOperationException($"Direcao da fila Telegram nao suportada: '{direction}'.")
        };
    }

    private static TimeSpan ResolveRetryDelay(int attemptCount)
    {
        return attemptCount switch
        {
            <= 0 => TimeSpan.FromSeconds(30),
            1 => TimeSpan.FromMinutes(2),
            2 => TimeSpan.FromMinutes(10),
            3 => TimeSpan.FromMinutes(30),
            _ => TimeSpan.FromHours(2)
        };
    }

    private static string FormatDirectionLabel(string direction)
    {
        return NormalizeDirection(direction) switch
        {
            TelegramDeliveryDirections.TelegramToChatwoot => "Telegram -> Chatwoot",
            TelegramDeliveryDirections.ChatwootToTelegram => "Chatwoot -> Telegram",
            _ => "Telegram"
        };
    }
}
