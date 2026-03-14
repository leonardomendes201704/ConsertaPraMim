using System.Text.Json;
using AppMobileCPM.Services;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Chatwoot;

public sealed class ChatwootBackfillService : IChatwootBackfillService
{
    private const int DefaultBatchSize = 20;
    private const int MaxBatchSize = 200;

    private readonly IAdminKanbanService _kanbanService;
    private readonly IChatwootLeadSyncService _chatwootLeadSyncService;
    private readonly ChatwootOptions _options;
    private readonly ILogger<ChatwootBackfillService> _logger;

    public ChatwootBackfillService(
        IAdminKanbanService kanbanService,
        IChatwootLeadSyncService chatwootLeadSyncService,
        IOptions<ChatwootOptions> options,
        ILogger<ChatwootBackfillService> logger)
    {
        _kanbanService = kanbanService;
        _chatwootLeadSyncService = chatwootLeadSyncService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ChatwootBackfillRunResult> RunAsync(ChatwootBackfillRunRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedBoardType = string.IsNullOrWhiteSpace(request.BoardType)
            ? null
            : AdminKanbanBoardTypes.Normalize(request.BoardType);
        var batchSize = Math.Clamp(request.BatchSize <= 0 ? DefaultBatchSize : request.BatchSize, 1, MaxBatchSize);
        var scopeKey = BuildScopeKey(normalizedBoardType);
        var scopeLabel = BuildScopeLabel(normalizedBoardType);
        var checkpoint = _kanbanService.GetChatwootBackfillCheckpoint(scopeKey);
        var effectiveStartAfterLeadId = request.StartAfterLeadId ?? checkpoint?.LastProcessedLeadId;
        var candidates = _kanbanService.ListChatwootBackfillCandidates(normalizedBoardType, effectiveStartAfterLeadId, batchSize);

        if (!request.DryRun && !_options.Enabled)
        {
            throw new InvalidOperationException("Integracao com Chatwoot desabilitada no ambiente atual.");
        }

        if (request.DryRun)
        {
            return BuildDryRunResult(
                scopeKey,
                scopeLabel,
                batchSize,
                effectiveStartAfterLeadId,
                checkpoint?.LastProcessedLeadId,
                candidates);
        }

        var startedAt = DateTime.UtcNow;
        var items = new List<ChatwootBackfillItemResult>(candidates.Count);
        var successCount = 0;
        var failedCount = 0;
        var pendingCount = 0;
        int? lastProcessedLeadId = null;

        SaveCheckpoint(
            scopeKey,
            checkpoint?.LastProcessedLeadId,
            startedAt,
            completedAt: null,
            lastRunStatus: "running",
            batchSize,
            totalSelected: candidates.Count,
            successCount: 0,
            failedCount: 0,
            pendingCount: 0,
            effectiveStartAfterLeadId: effectiveStartAfterLeadId);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var syncResult = await _chatwootLeadSyncService.SyncLeadAsync(candidate.LeadId, cancellationToken);
            var itemStatus = ResolveItemStatus(syncResult);

            switch (itemStatus)
            {
                case ChatwootBackfillItemStatuses.Synced:
                    successCount++;
                    break;
                case ChatwootBackfillItemStatuses.Pending:
                    pendingCount++;
                    break;
                default:
                    failedCount++;
                    break;
            }

            items.Add(new ChatwootBackfillItemResult
            {
                LeadId = candidate.LeadId,
                BoardType = candidate.BoardType,
                LeadName = candidate.LeadName,
                StageName = candidate.StageName,
                Status = itemStatus,
                Message = syncResult.Message,
                ContactId = syncResult.ContactId,
                ConversationId = syncResult.ConversationId,
                InboxId = syncResult.InboxId
            });

            lastProcessedLeadId = candidate.LeadId;
            SaveCheckpoint(
                scopeKey,
                lastProcessedLeadId,
                startedAt,
                completedAt: null,
                lastRunStatus: "running",
                batchSize,
                totalSelected: candidates.Count,
                successCount: successCount,
                failedCount: failedCount,
                pendingCount: pendingCount,
                effectiveStartAfterLeadId: effectiveStartAfterLeadId);
        }

        var completedAt = DateTime.UtcNow;
        var persistedCheckpoint = SaveCheckpoint(
            scopeKey,
            lastProcessedLeadId ?? checkpoint?.LastProcessedLeadId,
            startedAt,
            completedAt,
            ChatwootBackfillRunStatuses.Completed,
            batchSize,
            totalSelected: candidates.Count,
            successCount: successCount,
            failedCount: failedCount,
            pendingCount: pendingCount,
            effectiveStartAfterLeadId: effectiveStartAfterLeadId);

        _logger.LogInformation(
            "Backfill Chatwoot concluido. ScopeKey={ScopeKey} BatchSize={BatchSize} Total={Total} Success={Success} Failed={Failed} Pending={Pending} LastProcessedLeadId={LastProcessedLeadId}",
            scopeKey,
            batchSize,
            candidates.Count,
            successCount,
            failedCount,
            pendingCount,
            persistedCheckpoint.LastProcessedLeadId);

        return new ChatwootBackfillRunResult
        {
            ScopeKey = scopeKey,
            ScopeLabel = scopeLabel,
            DryRun = false,
            BatchSize = batchSize,
            EffectiveStartAfterLeadId = effectiveStartAfterLeadId,
            StoredCheckpointLeadId = checkpoint?.LastProcessedLeadId,
            LastProcessedLeadId = persistedCheckpoint.LastProcessedLeadId,
            TotalSelected = candidates.Count,
            SuccessCount = successCount,
            FailedCount = failedCount,
            PendingCount = pendingCount,
            Status = ChatwootBackfillRunStatuses.Completed,
            Items = items
        };
    }

    private static ChatwootBackfillRunResult BuildDryRunResult(
        string scopeKey,
        string scopeLabel,
        int batchSize,
        int? effectiveStartAfterLeadId,
        int? storedCheckpointLeadId,
        IReadOnlyList<AdminKanbanChatwootBackfillCandidateRecord> candidates)
    {
        var items = new List<ChatwootBackfillItemResult>(candidates.Count);
        var failedCount = 0;
        var pendingCount = 0;

        foreach (var candidate in candidates)
        {
            if (!HasMinimumContactData(candidate))
            {
                failedCount++;
                items.Add(new ChatwootBackfillItemResult
                {
                    LeadId = candidate.LeadId,
                    BoardType = candidate.BoardType,
                    LeadName = candidate.LeadName,
                    StageName = candidate.StageName,
                    Status = ChatwootBackfillItemStatuses.Failed,
                    Message = "Lead sem telefone ou e-mail valido para sincronizacao com Chatwoot.",
                    ContactId = candidate.ChatwootContactId,
                    InboxId = candidate.ChatwootInboxId
                });
                continue;
            }

            pendingCount++;
            items.Add(new ChatwootBackfillItemResult
            {
                LeadId = candidate.LeadId,
                BoardType = candidate.BoardType,
                LeadName = candidate.LeadName,
                StageName = candidate.StageName,
                Status = ChatwootBackfillItemStatuses.Pending,
                Message = candidate.ChatwootContactId.HasValue
                    ? "Lead elegivel para reaproveitar contato existente e vincular conversa no Chatwoot."
                    : "Lead elegivel para criar ou reaproveitar contato e vincular conversa no Chatwoot.",
                ContactId = candidate.ChatwootContactId,
                InboxId = candidate.ChatwootInboxId
            });
        }

        return new ChatwootBackfillRunResult
        {
            ScopeKey = scopeKey,
            ScopeLabel = scopeLabel,
            DryRun = true,
            BatchSize = batchSize,
            EffectiveStartAfterLeadId = effectiveStartAfterLeadId,
            StoredCheckpointLeadId = storedCheckpointLeadId,
            LastProcessedLeadId = null,
            TotalSelected = candidates.Count,
            SuccessCount = 0,
            FailedCount = failedCount,
            PendingCount = pendingCount,
            Status = ChatwootBackfillRunStatuses.DryRun,
            Items = items
        };
    }

    private AdminKanbanChatwootBackfillCheckpointRecord SaveCheckpoint(
        string scopeKey,
        int? lastProcessedLeadId,
        DateTime startedAt,
        DateTime? completedAt,
        string lastRunStatus,
        int batchSize,
        int totalSelected,
        int successCount,
        int failedCount,
        int pendingCount,
        int? effectiveStartAfterLeadId)
    {
        var summaryJson = JsonSerializer.Serialize(new
        {
            batchSize,
            totalSelected,
            successCount,
            failedCount,
            pendingCount,
            effectiveStartAfterLeadId,
            lastProcessedLeadId
        });

        return _kanbanService.SaveChatwootBackfillCheckpoint(new AdminKanbanChatwootBackfillCheckpointUpsertRequest
        {
            ScopeKey = scopeKey,
            LastProcessedLeadId = lastProcessedLeadId,
            LastRunStartedAt = startedAt,
            LastRunCompletedAt = completedAt,
            LastRunStatus = lastRunStatus,
            LastSummaryJson = summaryJson
        });
    }

    private static bool HasMinimumContactData(AdminKanbanChatwootBackfillCandidateRecord candidate) =>
        !string.IsNullOrWhiteSpace(candidate.Phone) ||
        !string.IsNullOrWhiteSpace(candidate.Email);

    private static string ResolveItemStatus(ChatwootLeadSyncResult result)
    {
        if (result.Succeeded)
        {
            return ChatwootBackfillItemStatuses.Synced;
        }

        if (result.QueuedForRetry || string.Equals(result.Status, ChatwootSyncStatuses.Pending, StringComparison.OrdinalIgnoreCase))
        {
            return ChatwootBackfillItemStatuses.Pending;
        }

        return ChatwootBackfillItemStatuses.Failed;
    }

    private static string BuildScopeKey(string? boardType) =>
        string.IsNullOrWhiteSpace(boardType)
            ? "all"
            : $"board:{AdminKanbanBoardTypes.Normalize(boardType)}";

    private static string BuildScopeLabel(string? boardType) =>
        string.IsNullOrWhiteSpace(boardType)
            ? "Todos os funis"
            : AdminKanbanBoardTypes.GetTitle(boardType);
}
