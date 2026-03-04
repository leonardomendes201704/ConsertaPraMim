using System.Security.Cryptography;
using System.Text;
using ConsertaPraMim.Web.TelegramBridge.Options;
using ConsertaPraMim.Web.TelegramBridge.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Tests.Unit.Services;

public class TelegramChatbotFeatureFlagServiceTests
{
    [Fact(DisplayName = "Telegram chatbot feature flag | Disabled | Deve bloquear quando rollout desligado")]
    public void Evaluate_ShouldDisable_WhenRolloutDisabled()
    {
        var service = CreateService(
            new TelegramChatbotRolloutOptions
            {
                Enabled = false
            },
            environmentName: "Development");

        var result = service.Evaluate(chatId: 1001);

        Assert.False(result.IsEnabled);
        Assert.Equal("rollout_not_enabled", result.ReasonCode);
    }

    [Fact(DisplayName = "Telegram chatbot feature flag | Allow list | Deve priorizar chat permitido sobre percentual")]
    public void Evaluate_ShouldEnable_WhenChatIsAllowListed()
    {
        const long chatId = 6614607033538827000;
        var service = CreateService(
            new TelegramChatbotRolloutOptions
            {
                Enabled = true,
                RolloutPercentage = 0,
                AllowedChatIds = [chatId]
            },
            environmentName: "Production");

        var result = service.Evaluate(chatId);

        Assert.True(result.IsEnabled);
        Assert.Equal("rollout_chat_allowlisted", result.ReasonCode);
    }

    [Fact(DisplayName = "Telegram chatbot feature flag | Percentage | Deve bloquear chat fora do bucket")]
    public void Evaluate_ShouldDisable_WhenChatOutsideRolloutPercentage()
    {
        const long chatId = 700123;
        var bucket = ComputeBucket(chatId);
        var percentage = Math.Max(0, bucket - 1);

        var service = CreateService(
            new TelegramChatbotRolloutOptions
            {
                Enabled = true,
                RolloutPercentage = percentage
            },
            environmentName: "Production");

        var result = service.Evaluate(chatId);

        Assert.False(result.IsEnabled);
        Assert.Equal("rollout_outside_percentage", result.ReasonCode);
        Assert.Equal(bucket, result.Bucket);
    }

    [Fact(DisplayName = "Telegram chatbot feature flag | Environment | Deve bloquear ambiente nao permitido")]
    public void Evaluate_ShouldDisable_WhenEnvironmentNotAllowed()
    {
        var service = CreateService(
            new TelegramChatbotRolloutOptions
            {
                Enabled = true,
                EnabledEnvironments = ["Production"]
            },
            environmentName: "Development");

        var result = service.Evaluate(chatId: 999);

        Assert.False(result.IsEnabled);
        Assert.Equal("rollout_not_enabled", result.ReasonCode);
    }

    private static TelegramChatbotFeatureFlagService CreateService(
        TelegramChatbotRolloutOptions options,
        string environmentName)
    {
        return new TelegramChatbotFeatureFlagService(
            Options.Create(options),
            new FakeWebHostEnvironment(environmentName));
    }

    private static int ComputeBucket(long chatId)
    {
        var raw = Encoding.UTF8.GetBytes(chatId.ToString());
        var hash = SHA256.HashData(raw);
        var value = BitConverter.ToUInt32(hash, 0);
        return (int)(value % 100) + 1;
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public FakeWebHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
            ApplicationName = "ConsertaPraMim.Tests";
            ContentRootPath = AppContext.BaseDirectory;
            WebRootPath = AppContext.BaseDirectory;
            ContentRootFileProvider = new NullFileProvider();
            WebRootFileProvider = new NullFileProvider();
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; }

        public string WebRootPath { get; set; }

        public IFileProvider WebRootFileProvider { get; set; }

        public string ContentRootPath { get; set; }

        public IFileProvider ContentRootFileProvider { get; set; }
    }
}
