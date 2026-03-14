namespace AppMobileCPM.Services;

public interface IAdminKanbanService
{
    AdminKanbanBoardData GetBoard(string boardType);
    IReadOnlyList<AdminKanbanStageRecord> GetStages(string boardType);
    AdminKanbanLeadDetailsRecord? GetLeadDetails(int leadId);
    int CreateLead(AdminKanbanLeadUpsertRequest request);
    AdminKanbanTelegramLeadUpsertResult UpsertTelegramLead(AdminKanbanTelegramLeadUpsertRequest request);
    int? FindLeadIdByTelegramChatbotConversationId(Guid chatbotConversationId);
    int? FindLeadIdByTelegramChatId(long telegramChatId);
    bool TouchTelegramLeadLink(int leadId, AdminKanbanTelegramLinkTouchRequest request);
    AdminKanbanTelegramDeliveryQueueItemRecord EnqueueTelegramDeliveryQueueItem(AdminKanbanTelegramDeliveryQueueEnqueueRequest request);
    IReadOnlyList<AdminKanbanTelegramDeliveryQueueItemRecord> AcquireDueTelegramDeliveryQueueItems(int batchSize, DateTime attemptStartedAtUtc, string workerInstance);
    AdminKanbanTelegramDeliveryQueueItemRecord? FinalizeTelegramDeliveryQueueItem(AdminKanbanTelegramDeliveryQueueFinalizeRequest request);
    AdminKanbanTelegramDeliveryQueueItemRecord? RequeueTelegramDeliveryQueueItem(int queueItemId, DateTime nextAttemptAtUtc, string workerInstance);
    AdminKanbanTelegramDiagnosticsSnapshot GetTelegramDiagnostics(string? boardType, int issueLimit, int queueLimit);
    bool UpdateLead(int leadId, AdminKanbanLeadUpsertRequest request);
    bool UpdateLeadChatwootSync(int leadId, AdminKanbanLeadChatwootSyncUpdateRequest request);
    int? FindLeadIdByChatwootConversationId(long conversationId);
    bool ApplyChatwootWebhookLeadUpdate(int leadId, AdminKanbanLeadWebhookUpdateRequest request);
    AdminKanbanChatwootWebhookEventRecord CreateOrGetChatwootWebhookEvent(AdminKanbanChatwootWebhookEventUpsertRequest request);
    bool CompleteChatwootWebhookEvent(int webhookEventId, string processStatus, string? errorMessage);
    AdminKanbanChatwootSyncQueueItemRecord EnqueueChatwootSyncQueueItem(AdminKanbanChatwootSyncQueueEnqueueRequest request);
    IReadOnlyList<AdminKanbanChatwootSyncQueueItemRecord> AcquireDueChatwootSyncQueueItems(int batchSize, DateTime attemptStartedAtUtc, string workerInstance);
    AdminKanbanChatwootSyncQueueItemRecord? FinalizeChatwootSyncQueueItem(AdminKanbanChatwootSyncQueueFinalizeRequest request);
    int CompleteActiveChatwootSyncQueueItems(int leadId, string? operationType, string finalStatus, string? lastError, DateTime completedAtUtc);
    int PurgeChatwootWebhookPayloads(DateTime receivedBeforeUtc, DateTime purgedAtUtc);
    IReadOnlyList<AdminKanbanChatwootBackfillCandidateRecord> ListChatwootBackfillCandidates(string? boardType, int? startAfterLeadId, int batchSize);
    AdminKanbanChatwootBackfillCheckpointRecord? GetChatwootBackfillCheckpoint(string scopeKey);
    AdminKanbanChatwootBackfillCheckpointRecord SaveChatwootBackfillCheckpoint(AdminKanbanChatwootBackfillCheckpointUpsertRequest request);
    AdminKanbanChatwootDiagnosticsSnapshot GetChatwootDiagnostics(string? boardType, int issueLimit, int queueLimit);
    bool SaveBoardOrder(AdminKanbanBoardOrderUpdateRequest request);
    bool AddHistoryNote(int leadId, string note);
    bool AddHistoryEvent(int leadId, string eventType, string description);
}
