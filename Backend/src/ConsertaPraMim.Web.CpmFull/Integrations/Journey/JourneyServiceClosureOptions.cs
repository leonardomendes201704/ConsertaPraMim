namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyServiceClosureOptions
{
    public const string SectionName = "JourneyServiceClosure";

    public bool Enabled { get; init; } = true;
    public int CompletionLinkExpirationHours { get; init; } = 168;
    public int ReviewLinkExpirationHours { get; init; } = 168;
    public int LowScoreThreshold { get; init; } = 2;
}
