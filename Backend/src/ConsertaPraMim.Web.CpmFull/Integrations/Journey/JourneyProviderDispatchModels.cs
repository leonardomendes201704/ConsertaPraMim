namespace AppMobileCPM.Integrations.Journey;

public sealed record class JourneyProviderDispatchRunResult
{
    public int ReadyCount { get; init; }
    public int WavesQueuedCount { get; init; }
    public int QueueProcessedCount { get; init; }
    public int ExpiredWavesCount { get; init; }
    public int ExhaustedJourneysCount { get; init; }
}

public sealed record class JourneyProviderDispatchQueuePayload
{
    public int LeadId { get; init; }
    public int JourneyId { get; init; }
    public int WaveNumber { get; init; }
    public string TargetKey { get; init; } = string.Empty;
    public Guid ProviderId { get; init; }
    public string ProviderName { get; init; } = string.Empty;
    public string ProviderEmail { get; init; } = string.Empty;
    public string ProviderPhone { get; init; } = string.Empty;
    public string RequestedCategory { get; init; } = string.Empty;
}
