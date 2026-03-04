namespace ConsertaPraMim.Web.TelegramBridge.Options;

public sealed class TelegramBridgeOptions
{
    public const string SectionName = "TelegramBridge";

    public string BotToken { get; set; } = string.Empty;

    public int PollingTimeoutSeconds { get; set; } = 25;

    public int IdleDelayMilliseconds { get; set; } = 750;

    public long MaxAttachmentBytes { get; set; } = 20 * 1024 * 1024;

    public int MaxMessagesPerConversation { get; set; } = 500;
}
