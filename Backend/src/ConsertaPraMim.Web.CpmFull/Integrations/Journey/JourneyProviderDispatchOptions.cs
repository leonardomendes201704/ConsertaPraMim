namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyProviderDispatchOptions
{
    public const string SectionName = "JourneyProviderDispatch";

    public bool Enabled { get; init; }
    public bool WorkerEnabled { get; init; } = true;
    public int WorkerIntervalSeconds { get; init; } = 30;
    public int WorkerBatchSize { get; init; } = 25;
    public int QueueBatchSize { get; init; } = 25;
    public int WaveSize { get; init; } = 5;
    public int MaxWaves { get; init; } = 3;
    public int AcceptanceTimeoutMinutes { get; init; } = 45;
    public int QueueMaxAttempts { get; init; } = 3;
    public string DispatchStrategy { get; init; } = "top_ranked_waves";
}
