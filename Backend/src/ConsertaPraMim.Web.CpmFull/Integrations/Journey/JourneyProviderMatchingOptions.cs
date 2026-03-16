namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyProviderMatchingOptions
{
    public const string SectionName = "JourneyProviderMatching";

    public bool Enabled { get; init; }
    public bool WorkerEnabled { get; init; } = true;
    public int WorkerIntervalSeconds { get; init; } = 30;
    public int WorkerBatchSize { get; init; } = 25;
    public int MaxCandidatesToPersist { get; init; } = 12;
    public string Timezone { get; init; } = "America/Sao_Paulo";
}
