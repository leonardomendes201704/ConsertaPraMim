namespace AppMobileCPM.Integrations.Journey;

public sealed record class JourneyStageAutomationRunResult
{
    public int ScannedCount { get; init; }
    public int UpdatedCount { get; init; }
    public int TimerEscalationCount { get; init; }
}
