namespace AppMobileCPM.Integrations.Chatwoot;

public sealed class ChatwootOptions
{
    public const string SectionName = "Chatwoot";

    public bool Enabled { get; init; }
    public string BaseUrl { get; init; } = string.Empty;
    public string ApiAccessToken { get; init; } = string.Empty;
    public int AccountId { get; init; }
    public long ClientsInboxId { get; init; }
    public long ProvidersInboxId { get; init; }
    public string WebhookSecret { get; init; } = string.Empty;
    public int RequestTimeoutSeconds { get; init; } = 15;
    public int MaxRetryAttempts { get; init; } = 3;
    public int RetryBaseDelayMs { get; init; } = 500;
    public bool RetryWorkerEnabled { get; init; } = true;
    public int RetryWorkerIntervalSeconds { get; init; } = 30;
    public int RetryWorkerBatchSize { get; init; } = 20;
    public int SyncQueueMaxAttempts { get; init; } = 10;

    public TimeSpan GetRequestTimeout() =>
        TimeSpan.FromSeconds(RequestTimeoutSeconds <= 0 ? 15 : RequestTimeoutSeconds);
}
