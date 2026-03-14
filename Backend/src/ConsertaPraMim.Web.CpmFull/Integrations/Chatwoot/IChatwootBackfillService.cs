namespace AppMobileCPM.Integrations.Chatwoot;

public interface IChatwootBackfillService
{
    Task<ChatwootBackfillRunResult> RunAsync(ChatwootBackfillRunRequest request, CancellationToken cancellationToken = default);
}
