namespace AppMobileCPM.Integrations.Telegram;

public interface ITelegramBridgeObservabilityClient
{
    Task<TelegramBridgeObservabilityResult> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
