namespace AppMobileCPM.Integrations.Chatwoot;

public static class ChatwootSyncStatuses
{
    public const string Synced = "synced";
    public const string Failed = "failed";
    public const string Pending = "pending";
    public const string Skipped = "skipped";
    public const string Disabled = "disabled";
    public const string NotFound = "not_found";
}

public sealed class ChatwootInboxSummary
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ChannelType { get; init; } = string.Empty;
}

public sealed class ChatwootConnectionCheckResult
{
    public bool IsReachable { get; init; }
    public required IReadOnlyList<ChatwootInboxSummary> Inboxes { get; init; }
}

public sealed class ChatwootContactSummary
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string Identifier { get; init; } = string.Empty;
    public IReadOnlyList<ChatwootContactInboxSummary> ContactInboxes { get; init; } = [];
}

public sealed class ChatwootContactInboxSummary
{
    public long InboxId { get; init; }
    public string InboxName { get; init; } = string.Empty;
    public string SourceId { get; init; } = string.Empty;
}

public sealed class ChatwootConversationSummary
{
    public long Id { get; init; }
    public long InboxId { get; init; }
    public string Status { get; init; } = string.Empty;
}

public sealed class ChatwootMessageSummary
{
    public long Id { get; init; }
    public bool Private { get; init; }
}

public sealed class ChatwootUpsertContactRequest
{
    public long InboxId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public string Identifier { get; init; } = string.Empty;
    public Dictionary<string, object?> AdditionalAttributes { get; init; } = [];
    public Dictionary<string, object?> CustomAttributes { get; init; } = [];
}

public sealed class ChatwootCreateContactInboxRequest
{
    public long InboxId { get; init; }
    public string SourceId { get; init; } = string.Empty;
}

public sealed class ChatwootCreateConversationRequest
{
    public string SourceId { get; init; } = string.Empty;
    public long InboxId { get; init; }
    public long ContactId { get; init; }
    public string Status { get; init; } = "open";
}

public sealed class ChatwootCreateMessageRequest
{
    public string Content { get; init; } = string.Empty;
    public string MessageType { get; init; } = "outgoing";
    public bool Private { get; init; } = true;
}

public sealed class ChatwootLeadSyncResult
{
    public bool Succeeded { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public long? ContactId { get; init; }
    public long? ConversationId { get; init; }
    public long? InboxId { get; init; }

    public static ChatwootLeadSyncResult Synced(string message, long? contactId, long? conversationId, long? inboxId) =>
        new()
        {
            Succeeded = true,
            Status = ChatwootSyncStatuses.Synced,
            Message = message,
            ContactId = contactId,
            ConversationId = conversationId,
            InboxId = inboxId
        };

    public static ChatwootLeadSyncResult Failed(string message, long? contactId, long? conversationId, long? inboxId) =>
        new()
        {
            Succeeded = false,
            Status = ChatwootSyncStatuses.Failed,
            Message = message,
            ContactId = contactId,
            ConversationId = conversationId,
            InboxId = inboxId
        };

    public static ChatwootLeadSyncResult Disabled(string message, long? contactId, long? conversationId, long? inboxId) =>
        new()
        {
            Succeeded = false,
            Status = ChatwootSyncStatuses.Disabled,
            Message = message,
            ContactId = contactId,
            ConversationId = conversationId,
            InboxId = inboxId
        };

    public static ChatwootLeadSyncResult NotFound(string message) =>
        new()
        {
            Succeeded = false,
            Status = ChatwootSyncStatuses.NotFound,
            Message = message
        };
}
