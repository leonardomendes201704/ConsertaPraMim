using AppMobileCPM.Services;

namespace AppMobileCPM.Integrations.Chatwoot;

public interface IChatwootSyncQueueService
{
    void EnqueueRetry(int leadId, string operationType, string reason, bool runImmediately = false);
    IReadOnlyList<AdminKanbanChatwootSyncQueueItemRecord> AcquireDueItems(string workerInstance, int batchSize, DateTime utcNow);
    string MarkProcessed(AdminKanbanChatwootSyncQueueItemRecord item, string workerInstance, string? note = null);
    string MarkFailed(AdminKanbanChatwootSyncQueueItemRecord item, string workerInstance, string errorMessage, bool retryRecommended);
    int CompleteActiveRetriesForLead(int leadId, IReadOnlyCollection<string> operationTypes, string? note = null);
    string ResolveOperationType(AdminKanbanLeadDetailsRecord lead);
}
