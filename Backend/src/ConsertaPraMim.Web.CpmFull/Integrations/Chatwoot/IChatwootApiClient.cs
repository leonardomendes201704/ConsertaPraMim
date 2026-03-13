namespace AppMobileCPM.Integrations.Chatwoot;

public interface IChatwootApiClient
{
    Task<ChatwootConnectionCheckResult> CheckConnectionAsync(CancellationToken cancellationToken = default);
}
