namespace ConsertaPraMim.Infrastructure.Configuration;

public sealed class GoogleCalendarSyncOptions
{
    public const string SectionName = "GoogleCalendarSync";

    public bool Enabled { get; set; }
    public bool RetryEnabled { get; set; } = true;
    public int RetryMaxAttempts { get; set; } = 5;
    public int RetryBaseDelaySeconds { get; set; } = 30;
    public int RetryMaxDelaySeconds { get; set; } = 900;
    public int RetryJitterMaxSeconds { get; set; } = 20;
    public bool RetryWorkerEnabled { get; set; } = true;
    public int RetryWorkerIntervalSeconds { get; set; } = 30;
    public int RetryWorkerBatchSize { get; set; } = 100;
    public string ProjectId { get; set; } = string.Empty;
    public string ServiceAccountEmail { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string CalendarId { get; set; } = string.Empty;
    public string Timezone { get; set; } = "America/Sao_Paulo";
}
