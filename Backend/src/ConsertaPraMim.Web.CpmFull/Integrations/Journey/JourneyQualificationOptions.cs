namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyQualificationOptions
{
    public const string SectionName = "JourneyQualification";

    public bool Enabled { get; init; } = true;
    public bool AiEnabled { get; init; }
    public string OpenAiApiKey { get; init; } = string.Empty;
    public string OpenAiModel { get; init; } = "gpt-4.1-mini";
    public int RequestTimeoutSeconds { get; init; } = 20;
    public int MaxRetries { get; init; } = 1;
    public decimal MinimumConfidenceForAutoApply { get; init; } = 0.72m;

    public TimeSpan GetRequestTimeout() =>
        TimeSpan.FromSeconds(RequestTimeoutSeconds <= 0 ? 20 : RequestTimeoutSeconds);
}
