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
    public string TelegramBridgeBaseUrl { get; init; } = string.Empty;
    public string SharedSecret { get; init; } = string.Empty;
    public int RequestTimeoutSeconds { get; init; } = 15;
    public bool DeliveryWorkerEnabled { get; init; } = true;
    public int DeliveryWorkerIntervalSeconds { get; init; } = 20;
    public int DeliveryWorkerBatchSize { get; init; } = 20;
    public int DeliveryQueueMaxAttempts { get; init; } = 10;

    public IReadOnlyList<string> GetAllowedBotSources() =>
        AllowedBotSources
            .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public TimeSpan GetRequestTimeout() =>
        TimeSpan.FromSeconds(RequestTimeoutSeconds <= 0 ? 15 : RequestTimeoutSeconds);
}
