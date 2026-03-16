namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyAutomationOptions
{
    public const string SectionName = "JourneyAutomation";

    public bool Enabled { get; init; }
    public bool ClientsAutomationEnabled { get; init; } = true;
    public bool ProvidersAutomationEnabled { get; init; } = true;
    public string SharedSecret { get; init; } = string.Empty;
}
