namespace ConsertaPraMim.Web.TelegramBridge.Options;

public sealed class TelegramChatbotObservabilityOptions
{
    public const string SectionName = "TelegramChatbotObservability";

    public bool EnableDashboardEndpoint { get; set; } = true;

    public bool AllowDashboardWithoutTokenInDevelopment { get; set; } = true;

    public string DashboardToken { get; set; } = string.Empty;
}
