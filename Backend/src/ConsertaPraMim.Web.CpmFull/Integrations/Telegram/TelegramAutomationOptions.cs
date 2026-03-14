namespace AppMobileCPM.Integrations.Telegram;

public sealed class TelegramAutomationOptions
{
    public const string SectionName = "TelegramAutomation";

    public bool Enabled { get; init; }
    public bool ClientsAutomationEnabled { get; init; } = true;
    public bool ProvidersAutomationEnabled { get; init; }
    public bool MirrorMessagesEnabled { get; init; }
    public bool RequireHumanHandoffForOutbound { get; init; } = true;
    public string AllowedBotSources { get; init; } = "telegram";
    public string SharedSecret { get; init; } = string.Empty;

    public IReadOnlyList<string> GetAllowedBotSources() =>
        AllowedBotSources
            .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
