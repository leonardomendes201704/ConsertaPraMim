namespace AppMobileCPM.Integrations.Telegram;

public interface ITelegramLeadAutomationService
{
    Task<TelegramLeadAutomationResult> UpsertLeadAsync(
        TelegramLeadAutomationRequest request,
        string providedSecret,
        CancellationToken cancellationToken = default);
}
