namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyProviderMatchingRunResult
{
    public int ScannedCount { get; init; }
    public int UpdatedCount { get; init; }
    public int EligibleJourneysCount { get; init; }
    public int NoCoverageJourneysCount { get; init; }
}
