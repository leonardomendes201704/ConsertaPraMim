using AppMobileCPM.Services;

namespace AppMobileCPM.Integrations.Telegram;

public interface ITelegramDeliveryQueueService
{
    AdminKanbanTelegramDeliveryQueueItemRecord Enqueue(
        int leadId,
        string direction,
        string deliveryKey,
        string payloadJson,
        long? chatwootConversationId,
        long? telegramChatId,
        string reason,
        bool runImmediately = false);

    IReadOnlyList<AdminKanbanTelegramDeliveryQueueItemRecord> AcquireDueItems(string workerInstance, int batchSize, DateTime utcNow);
    string MarkProcessed(AdminKanbanTelegramDeliveryQueueItemRecord item, string workerInstance, string? note = null);
    string MarkFailed(AdminKanbanTelegramDeliveryQueueItemRecord item, string workerInstance, string errorMessage, bool retryRecommended);
}
