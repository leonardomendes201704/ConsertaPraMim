namespace AppMobileCPM.Integrations.Telegram;

public interface ITelegramBridgeDeliveryClient
{
    Task<TelegramBridgeHumanReplyResult> SendHumanReplyAsync(
        TelegramBridgeHumanReplyRequest request,
        CancellationToken cancellationToken = default);

    Task<TelegramBridgeSetHandoffResult> ActivateHumanHandoffAsync(
        TelegramBridgeSetHandoffRequest request,
        CancellationToken cancellationToken = default);

    Task<TelegramBridgeSetHandoffResult> ResumeBotAsync(
        TelegramBridgeSetHandoffRequest request,
        CancellationToken cancellationToken = default);

    Task<TelegramBridgeResetHandoffResult> ResetHumanHandoffAsync(
        TelegramBridgeResetHandoffRequest request,
        CancellationToken cancellationToken = default);
}
