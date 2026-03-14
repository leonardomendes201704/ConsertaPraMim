namespace AppMobileCPM.Services;

public static class AdminKanbanBoardTypes
{
    public const string Clients = "clientes";
    public const string Providers = "prestadores";

    public static bool IsValid(string? boardType) =>
        string.Equals(boardType, Clients, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(boardType, Providers, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? boardType)
    {
        if (string.Equals(boardType, Clients, StringComparison.OrdinalIgnoreCase))
        {
            return Clients;
        }

        if (string.Equals(boardType, Providers, StringComparison.OrdinalIgnoreCase))
        {
            return Providers;
        }

        throw new ArgumentException("Tipo de funil invalido.", nameof(boardType));
    }

    public static string GetTitle(string boardType) =>
        Normalize(boardType) switch
        {
            Clients => "Funil de Atendimento - Clientes",
            Providers => "Onboarding e Contato - Prestadores",
            _ => "Funil"
        };

    public static string GetSubtitle(string boardType) =>
        Normalize(boardType) switch
        {
            Clients => "Gerencie o ciclo de atendimento desde o primeiro contato ate a conclusao.",
            Providers => "Acompanhe o onboarding, validacao e ativacao de prestadores na plataforma.",
            _ => "Gerencie seu funil"
        };
}

public sealed class AdminKanbanBoardData
{
    public required string BoardType { get; init; }
    public required IReadOnlyList<AdminKanbanStageRecord> Stages { get; init; }
}

public sealed class AdminKanbanStageRecord
{
    public int Id { get; init; }
    public required string BoardType { get; init; }
    public required string Name { get; init; }
    public string Color { get; init; } = "#0d6efd";
    public int SortOrder { get; init; }
    public required IReadOnlyList<AdminKanbanLeadCardRecord> Leads { get; init; }
}

public sealed class AdminKanbanLeadCardRecord
{
    public int Id { get; init; }
    public int StageId { get; init; }
    public required string BoardType { get; init; }
    public required string Name { get; init; }
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string ServiceCategory { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Priority { get; init; } = "normal";
    public string StatusNote { get; init; } = string.Empty;
    public string ChatwootSyncStatus { get; init; } = string.Empty;
    public DateTime StageEnteredAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? LastContactAt { get; init; }
}

public sealed class AdminKanbanLeadDetailsRecord
{
    public int Id { get; init; }
    public int StageId { get; init; }
    public required string StageName { get; init; }
    public required string BoardType { get; init; }
    public required string Name { get; init; }
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string ServiceCategory { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Priority { get; init; } = "normal";
    public string StatusNote { get; init; } = string.Empty;
    public string InternalNotes { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? LastContactAt { get; init; }
    public AdminKanbanLeadChatwootSyncRecord Chatwoot { get; init; } = new();
    public required IReadOnlyList<AdminKanbanLeadHistoryRecord> History { get; init; }
}

public sealed class AdminKanbanLeadChatwootSyncRecord
{
    public long? ContactId { get; init; }
    public long? ConversationId { get; init; }
    public long? InboxId { get; init; }
    public string SyncStatus { get; init; } = string.Empty;
    public DateTime? LastSyncAt { get; init; }
    public string LastError { get; init; } = string.Empty;
}

public sealed class AdminKanbanLeadHistoryRecord
{
    public int Id { get; init; }
    public required string EventType { get; init; }
    public int? FromStageId { get; init; }
    public string FromStageName { get; init; } = string.Empty;
    public int? ToStageId { get; init; }
    public string ToStageName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed class AdminKanbanLeadUpsertRequest
{
    public required string BoardType { get; init; }
    public int StageId { get; init; }
    public required string Name { get; init; }
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string ServiceCategory { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Priority { get; init; } = "normal";
    public string StatusNote { get; init; } = string.Empty;
    public string InternalNotes { get; init; } = string.Empty;
    public DateTime? LastContactAt { get; init; }
}

public sealed class AdminKanbanBoardOrderUpdateRequest
{
    public required string BoardType { get; init; }
    public int? ChangedLeadId { get; init; }
    public int? FromStageId { get; init; }
    public int? ToStageId { get; init; }
    public required IReadOnlyList<AdminKanbanStageOrderUpdateItem> Stages { get; init; }
}

public sealed class AdminKanbanStageOrderUpdateItem
{
    public int StageId { get; init; }
    public required IReadOnlyList<int> LeadIds { get; init; }
}

public sealed class AdminKanbanLeadChatwootSyncUpdateRequest
{
    public long? ChatwootContactId { get; init; }
    public long? ChatwootConversationId { get; init; }
    public long? ChatwootInboxId { get; init; }
    public string? ChatwootSyncStatus { get; init; }
    public DateTime? ChatwootLastSyncAt { get; init; }
    public string? ChatwootLastError { get; init; }
    public bool ClearChatwootLastError { get; init; }
}

public sealed class AdminKanbanLeadWebhookUpdateRequest
{
    public DateTime? LastContactAt { get; init; }
    public string HistoryEventType { get; init; } = string.Empty;
    public string HistoryDescription { get; init; } = string.Empty;
}

public sealed class AdminKanbanChatwootWebhookEventUpsertRequest
{
    public string? ProviderEventId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public long? ConversationId { get; init; }
    public string PayloadJson { get; init; } = string.Empty;
    public string Signature { get; init; } = string.Empty;
    public DateTime ReceivedAt { get; init; }
}

public sealed record class AdminKanbanChatwootWebhookEventRecord
{
    public int Id { get; init; }
    public string ProviderEventId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public long? ConversationId { get; init; }
    public string ProcessStatus { get; init; } = string.Empty;
    public DateTime ReceivedAt { get; init; }
    public DateTime? ProcessedAt { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public bool IsDuplicate { get; init; }
}

public sealed class AdminKanbanChatwootSyncQueueEnqueueRequest
{
    public int LeadId { get; init; }
    public string OperationType { get; init; } = string.Empty;
    public DateTime NextAttemptAt { get; init; }
    public int MaxAttempts { get; init; }
    public string? LastError { get; init; }
}

public sealed class AdminKanbanChatwootSyncQueueFinalizeRequest
{
    public int QueueItemId { get; init; }
    public string FinalStatus { get; init; } = string.Empty;
    public DateTime FinalizedAt { get; init; }
    public DateTime? NextAttemptAt { get; init; }
    public string? LastError { get; init; }
    public bool ClearLastError { get; init; }
    public string WorkerInstance { get; init; } = string.Empty;
}

public sealed record class AdminKanbanChatwootSyncQueueItemRecord
{
    public int Id { get; init; }
    public int LeadId { get; init; }
    public string OperationType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int AttemptCount { get; init; }
    public int MaxAttempts { get; init; }
    public DateTime NextAttemptAt { get; init; }
    public DateTime? LastAttemptAt { get; init; }
    public string LastError { get; init; } = string.Empty;
    public string WorkerInstance { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime? ProcessedAt { get; init; }
    public DateTime? DeadLetterAt { get; init; }
}

public sealed record class AdminKanbanChatwootBackfillCandidateRecord
{
    public int LeadId { get; init; }
    public string BoardType { get; init; } = string.Empty;
    public string StageName { get; init; } = string.Empty;
    public string LeadName { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public long? ChatwootContactId { get; init; }
    public long? ChatwootInboxId { get; init; }
}

public sealed record class AdminKanbanChatwootBackfillCheckpointRecord
{
    public string ScopeKey { get; init; } = string.Empty;
    public int? LastProcessedLeadId { get; init; }
    public DateTime? LastRunStartedAt { get; init; }
    public DateTime? LastRunCompletedAt { get; init; }
    public string LastRunStatus { get; init; } = string.Empty;
    public string LastSummaryJson { get; init; } = string.Empty;
    public DateTime UpdatedAt { get; init; }
}

public sealed record class AdminKanbanChatwootSyncIssueRecord
{
    public int LeadId { get; init; }
    public string BoardType { get; init; } = string.Empty;
    public string StageName { get; init; } = string.Empty;
    public string LeadName { get; init; } = string.Empty;
    public string SyncStatus { get; init; } = string.Empty;
    public DateTime? LastSyncAt { get; init; }
    public string LastError { get; init; } = string.Empty;
    public long? ContactId { get; init; }
    public long? ConversationId { get; init; }
    public long? InboxId { get; init; }
}

public sealed record class AdminKanbanChatwootQueueDiagnosticRecord
{
    public int QueueItemId { get; init; }
    public int LeadId { get; init; }
    public string BoardType { get; init; } = string.Empty;
    public string StageName { get; init; } = string.Empty;
    public string LeadName { get; init; } = string.Empty;
    public string OperationType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int AttemptCount { get; init; }
    public int MaxAttempts { get; init; }
    public DateTime NextAttemptAt { get; init; }
    public DateTime? LastAttemptAt { get; init; }
    public string LastError { get; init; } = string.Empty;
    public long? ConversationId { get; init; }
}

public sealed record class AdminKanbanChatwootDiagnosticsSnapshot
{
    public string ScopeBoardType { get; init; } = string.Empty;
    public int TotalLeads { get; init; }
    public int SyncedCount { get; init; }
    public int PendingCount { get; init; }
    public int FailedCount { get; init; }
    public int ActiveQueueCount { get; init; }
    public int DeadLetterCount { get; init; }
    public IReadOnlyList<AdminKanbanChatwootSyncIssueRecord> RecentIssues { get; init; } = [];
    public IReadOnlyList<AdminKanbanChatwootQueueDiagnosticRecord> RecentQueueItems { get; init; } = [];
}

public sealed class AdminKanbanChatwootBackfillCheckpointUpsertRequest
{
    public string ScopeKey { get; init; } = string.Empty;
    public int? LastProcessedLeadId { get; init; }
    public DateTime? LastRunStartedAt { get; init; }
    public DateTime? LastRunCompletedAt { get; init; }
    public string LastRunStatus { get; init; } = string.Empty;
    public string LastSummaryJson { get; init; } = string.Empty;
}
