namespace AppMobileCPM.Integrations.Chatwoot;

public interface IChatwootLeadSyncService
{
    Task<ChatwootLeadSyncResult> SyncLeadAsync(int leadId, CancellationToken cancellationToken = default, bool queueOnFailure = true);
    Task<ChatwootLeadSyncResult> SyncLeadStageAsync(int leadId, CancellationToken cancellationToken = default, bool queueOnFailure = true);
}
