namespace ConsertaPraMim.Infrastructure.Configuration;

public sealed class GoogleCalendarSyncOptions
{
    public const string SectionName = "GoogleCalendarSync";

    public bool Enabled { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public string ServiceAccountEmail { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string CalendarId { get; set; } = string.Empty;
    public string Timezone { get; set; } = "America/Sao_Paulo";
}
