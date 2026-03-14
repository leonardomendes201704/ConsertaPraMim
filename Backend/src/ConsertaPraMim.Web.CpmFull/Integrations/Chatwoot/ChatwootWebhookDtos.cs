namespace AppMobileCPM.Integrations.Chatwoot;

public sealed class ChatwootWebhookRequest
{
    public byte[] RawBody { get; init; } = [];
    public string Signature { get; init; } = string.Empty;
    public string Timestamp { get; init; } = string.Empty;
    public string DeliveryId { get; init; } = string.Empty;
}

public sealed class ChatwootWebhookProcessResult
{
    public int HttpStatusCode { get; init; }
    public bool Accepted { get; init; }
    public string ProcessStatus { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public long? ConversationId { get; init; }
    public int? LeadId { get; init; }
    public int? WebhookEventId { get; init; }
    public bool IsDuplicate { get; init; }
}
