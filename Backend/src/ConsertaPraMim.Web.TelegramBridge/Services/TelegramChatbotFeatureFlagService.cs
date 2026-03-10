using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ConsertaPraMim.Web.TelegramBridge.Options;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramChatbotFeatureFlagService : ITelegramChatbotFeatureFlagService
{
    private readonly IOptions<TelegramChatbotRolloutOptions> _options;
    private readonly IWebHostEnvironment _environment;

    public TelegramChatbotFeatureFlagService(
        IOptions<TelegramChatbotRolloutOptions> options,
        IWebHostEnvironment environment)
    {
        _options = options;
        _environment = environment;
    }

    public TelegramChatbotFeatureFlagDecision Evaluate(long chatId)
    {
        var options = _options.Value;
        if (!options.Enabled)
        {
            return new TelegramChatbotFeatureFlagDecision(
                IsEnabled: false,
                ReasonCode: "rollout_not_enabled");
        }

        if (options.BlockedChatIds.Contains(chatId))
        {
            return new TelegramChatbotFeatureFlagDecision(
                IsEnabled: false,
                ReasonCode: "rollout_chat_blocked");
        }

        if (options.AllowedChatIds.Contains(chatId))
        {
            return new TelegramChatbotFeatureFlagDecision(
                IsEnabled: true,
                ReasonCode: "rollout_chat_allowlisted",
                Bucket: 0);
        }

        if (!IsEnvironmentAllowed(options))
        {
            return new TelegramChatbotFeatureFlagDecision(
                IsEnabled: false,
                ReasonCode: "rollout_not_enabled");
        }

        var percentage = Math.Clamp(options.RolloutPercentage, 0, 100);
        if (percentage >= 100)
        {
            return new TelegramChatbotFeatureFlagDecision(
                IsEnabled: true,
                ReasonCode: "rollout_full_release",
                Bucket: 100);
        }

        if (percentage <= 0)
        {
            return new TelegramChatbotFeatureFlagDecision(
                IsEnabled: false,
                ReasonCode: "rollout_outside_percentage",
                Bucket: 100);
        }

        var bucket = ComputeBucket(chatId);
        return bucket <= percentage
            ? new TelegramChatbotFeatureFlagDecision(true, "rollout_in_percentage", bucket)
            : new TelegramChatbotFeatureFlagDecision(false, "rollout_outside_percentage", bucket);
    }

    private bool IsEnvironmentAllowed(TelegramChatbotRolloutOptions options)
    {
        if (options.EnabledEnvironments.Count > 0)
        {
            return options.EnabledEnvironments.Any(item =>
                item.Equals(_environment.EnvironmentName, StringComparison.OrdinalIgnoreCase));
        }

        if (_environment.IsDevelopment())
        {
            return options.EnableInDevelopment;
        }

        return true;
    }

    private static int ComputeBucket(long chatId)
    {
        var input = chatId.ToString(CultureInfo.InvariantCulture);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var rawValue = BitConverter.ToUInt32(hash, 0);
        return (int)(rawValue % 100) + 1;
    }
}
