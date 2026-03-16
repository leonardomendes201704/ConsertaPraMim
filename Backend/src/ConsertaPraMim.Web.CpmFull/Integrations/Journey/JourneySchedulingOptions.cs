namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneySchedulingOptions
{
    public const string SectionName = "JourneyScheduling";

    public bool Enabled { get; init; }
    public string ProjectId { get; init; } = string.Empty;
    public string ServiceAccountEmail { get; init; } = string.Empty;
    public string PrivateKey { get; init; } = string.Empty;
    public string CalendarId { get; init; } = string.Empty;
    public string Timezone { get; init; } = "America/Sao_Paulo";
    public string BusinessHoursStartLocal { get; init; } = "08:00";
    public string BusinessHoursEndLocal { get; init; } = "18:00";
    public bool SaturdayEnabled { get; init; } = true;
    public bool SundayEnabled { get; init; }
    public int SlotDurationMinutes { get; init; } = 120;
    public int SuggestionCount { get; init; } = 3;
    public int SuggestionWindowDays { get; init; } = 7;
    public int MinimumNoticeMinutes { get; init; } = 120;
    public int RequestTimeoutSeconds { get; init; } = 30;
    public int TokenRefreshSafetyMinutes { get; init; } = 5;
}
