namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyStageAutomationOptions
{
    public const string SectionName = "JourneyStageAutomation";

    public bool Enabled { get; init; }
    public bool WorkerEnabled { get; init; } = true;
    public int WorkerIntervalSeconds { get; init; } = 20;
    public int WorkerBatchSize { get; init; } = 50;
    public int PendingDataTimeoutMinutes { get; init; } = 120;
    public int ScheduleConfirmationTimeoutMinutes { get; init; } = 180;
    public int ProviderAcceptanceTimeoutMinutes { get; init; } = 45;
    public int ClientReviewTimeoutHours { get; init; } = 72;
    public int ProviderReviewTimeoutHours { get; init; } = 72;
}
