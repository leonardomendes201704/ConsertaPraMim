namespace AppMobileCPM.Integrations.Telegram;

public interface ITelegramBridgeDeliveryClient
{
    Task<TelegramBridgeHumanReplyResult> SendHumanReplyAsync(
        TelegramBridgeHumanReplyRequest request,
        CancellationToken cancellationToken = default);
}
