namespace AppMobileCPM.Integrations.Chatwoot;

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
