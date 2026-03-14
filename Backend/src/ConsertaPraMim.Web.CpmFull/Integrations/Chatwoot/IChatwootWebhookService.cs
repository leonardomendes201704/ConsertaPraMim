namespace AppMobileCPM.Integrations.Chatwoot;

public interface IChatwootWebhookService
{
    Task<ChatwootWebhookProcessResult> HandleAsync(ChatwootWebhookRequest request, CancellationToken cancellationToken = default);
}
