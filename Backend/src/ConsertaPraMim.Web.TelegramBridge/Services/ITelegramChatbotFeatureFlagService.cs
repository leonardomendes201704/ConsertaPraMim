namespace ConsertaPraMim.Web.TelegramBridge.Services;

public interface ITelegramChatbotFeatureFlagService
{
    TelegramChatbotFeatureFlagDecision Evaluate(long chatId);
}

public sealed record TelegramChatbotFeatureFlagDecision(
    bool IsEnabled,
    string ReasonCode,
    int? Bucket = null);
