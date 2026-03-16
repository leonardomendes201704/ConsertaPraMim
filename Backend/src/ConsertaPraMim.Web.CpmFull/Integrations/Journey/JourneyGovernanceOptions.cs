namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyGovernanceOptions
{
    public const string SectionName = "JourneyGovernance";

    public bool Enabled { get; init; } = true;
    public int RolloutPercentage { get; init; } = 100;
    public string AllowedSourceChannels { get; init; } = "landing,telegram,service_request";
    public bool IntakeEnabled { get; init; } = true;
    public bool StageAutomationEnabled { get; init; } = true;
    public bool MatchingEnabled { get; init; } = true;
    public bool DispatchEnabled { get; init; } = true;
    public bool ConnectionEnabled { get; init; } = true;
    public bool ClosureEnabled { get; init; } = true;
    public bool RouteOperationalExceptionsToHandoff { get; init; } = true;
}
