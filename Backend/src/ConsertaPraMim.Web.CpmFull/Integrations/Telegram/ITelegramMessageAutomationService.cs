using AppMobileCPM.Services;

namespace AppMobileCPM.Integrations.Telegram;

public interface ITelegramMessageAutomationService
{
    Task<TelegramInboundMessageAutomationResult> EnqueueInboundMessageAsync(
        TelegramInboundMessageAutomationRequest request,
        string providedSecret,
        CancellationToken cancellationToken = default);

    Task<bool> TryEnqueueOutboundMessageFromChatwootAsync(
        AdminKanbanLeadDetailsRecord lead,
        long? chatwootMessageId,
        string messageText,
        string senderName,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default);

    Task<TelegramDeliveryProcessResult> ProcessQueueItemAsync(
        AdminKanbanTelegramDeliveryQueueItemRecord item,
        CancellationToken cancellationToken = default);
}
