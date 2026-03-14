namespace ConsertaPraMim.Web.TelegramBridge.Options;

public sealed class TelegramAutomationOptions
{
    public const string SectionName = "TelegramAutomation";

    public bool Enabled { get; init; }
    public bool ClientsAutomationEnabled { get; init; } = true;
    public bool ProvidersAutomationEnabled { get; init; }
    public bool MirrorMessagesEnabled { get; init; }
    public bool RequireHumanHandoffForOutbound { get; init; } = true;
    public string AllowedBotSources { get; init; } = "telegram";
    public string CpmFullBaseUrl { get; init; } = string.Empty;
    public string SharedSecret { get; init; } = string.Empty;
    public int RequestTimeoutSeconds { get; init; } = 15;

    public TimeSpan GetRequestTimeout() =>
        TimeSpan.FromSeconds(RequestTimeoutSeconds <= 0 ? 15 : RequestTimeoutSeconds);
}
