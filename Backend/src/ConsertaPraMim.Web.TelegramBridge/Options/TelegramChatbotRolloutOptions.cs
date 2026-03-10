namespace ConsertaPraMim.Web.TelegramBridge.Options;

public sealed class TelegramChatbotRolloutOptions
{
    public const string SectionName = "TelegramChatbotRollout";

    public bool Enabled { get; set; } = true;

    public int RolloutPercentage { get; set; } = 100;

    public bool EnableInDevelopment { get; set; } = true;

    public List<string> EnabledEnvironments { get; set; } = [];

    public List<long> AllowedChatIds { get; set; } = [];

    public List<long> BlockedChatIds { get; set; } = [];
}
