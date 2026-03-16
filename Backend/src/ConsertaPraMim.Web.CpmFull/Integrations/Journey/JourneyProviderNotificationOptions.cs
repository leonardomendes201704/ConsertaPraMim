namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyProviderNotificationOptions
{
    public const string SectionName = "JourneyProviderNotification";

    public bool Enabled { get; init; }
    public bool EmailEnabled { get; init; } = true;
    public string EmailTransport { get; init; } = "log";
    public string PublicBaseUrl { get; init; } = string.Empty;
    public string LinkSigningSecret { get; init; } = string.Empty;
    public int LinkExpirationMinutes { get; init; } = 45;
    public bool OpenTrackingEnabled { get; init; } = true;
    public string SenderEmail { get; init; } = string.Empty;
    public string SenderDisplayName { get; init; } = "ConsertaPraMim";
    public string SmtpHost { get; init; } = string.Empty;
    public int SmtpPort { get; init; } = 587;
    public bool SmtpUseSsl { get; init; } = true;
    public string SmtpUsername { get; init; } = string.Empty;
    public string SmtpPassword { get; init; } = string.Empty;
    public string ProviderPortalBaseUrl { get; init; } = string.Empty;
}
