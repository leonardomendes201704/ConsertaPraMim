namespace AppMobileCPM.Integrations.Chatwoot;

public interface IChatwootLeadSyncService
{
    Task<ChatwootLeadSyncResult> SyncLeadAsync(int leadId, CancellationToken cancellationToken = default);
    Task<ChatwootLeadSyncResult> SyncLeadStageAsync(int leadId, CancellationToken cancellationToken = default);
}
