namespace ConsertaPraMim.Web.TelegramBridge.Options;

public sealed class TelegramBridgeOptions
{
    public const string SectionName = "TelegramBridge";
    public const string LongPollingTransport = "LongPolling";
    public const string WebhookTransport = "Webhook";

    public string BotToken { get; set; } = string.Empty;

    public string UpdateTransport { get; set; } = LongPollingTransport;

    public int PollingTimeoutSeconds { get; set; } = 25;

    public int IdleDelayMilliseconds { get; set; } = 750;

    public string WebhookPublicBaseUrl { get; set; } = string.Empty;

    public string WebhookPath { get; set; } = "/api/integrations/telegram/webhook";

    public string WebhookSecretToken { get; set; } = string.Empty;

    public bool WebhookDropPendingUpdates { get; set; }

    public long MaxAttachmentBytes { get; set; } = 20 * 1024 * 1024;

    public int MaxMessagesPerConversation { get; set; } = 500;

    public bool AttachmentRetentionEnabled { get; set; } = true;

    public int AttachmentRetentionDays { get; set; } = 14;

    public int AttachmentRetentionIntervalMinutes { get; set; } = 360;

    public bool UsesWebhookTransport() =>
        string.Equals(UpdateTransport?.Trim(), WebhookTransport, StringComparison.OrdinalIgnoreCase);

    public bool UsesLongPollingTransport() =>
        !UsesWebhookTransport();

    public string BuildWebhookUrl()
    {
        if (!Uri.TryCreate(WebhookPublicBaseUrl?.Trim(), UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException("TelegramBridge:WebhookPublicBaseUrl invalido para o modo webhook.");
        }

        var normalizedPath = string.IsNullOrWhiteSpace(WebhookPath)
            ? "/api/integrations/telegram/webhook"
            : WebhookPath.Trim();

        if (!normalizedPath.StartsWith("/", StringComparison.Ordinal))
        {
            normalizedPath = "/" + normalizedPath;
        }

        return new Uri(baseUri, normalizedPath).ToString();
    }
}
