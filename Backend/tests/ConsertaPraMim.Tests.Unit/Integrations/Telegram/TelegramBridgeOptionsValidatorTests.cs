using ConsertaPraMim.Web.TelegramBridge.Options;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Tests.Unit.Integrations.Telegram;

public sealed class TelegramBridgeOptionsValidatorTests
{
    [Fact(DisplayName = "Telegram Bridge Options | Deve aceitar configuracao valida")]
    public void Validate_DeveAceitarConfiguracaoValida()
    {
        var validator = new TelegramBridgeOptionsValidator();
        var result = validator.Validate(Options.DefaultName, new TelegramBridgeOptions
        {
            PollingTimeoutSeconds = 25,
            IdleDelayMilliseconds = 750,
            MaxAttachmentBytes = 20 * 1024 * 1024,
            MaxMessagesPerConversation = 500,
            AttachmentRetentionEnabled = true,
            AttachmentRetentionDays = 14,
            AttachmentRetentionIntervalMinutes = 360
        });

        Assert.True(result.Succeeded);
    }

    [Fact(DisplayName = "Telegram Bridge Options | Deve falhar com retention invalida")]
    public void Validate_DeveFalharComRetentionInvalida()
    {
        var validator = new TelegramBridgeOptionsValidator();
        var result = validator.Validate(Options.DefaultName, new TelegramBridgeOptions
        {
            PollingTimeoutSeconds = 25,
            IdleDelayMilliseconds = 750,
            MaxAttachmentBytes = 20 * 1024 * 1024,
            MaxMessagesPerConversation = 500,
            AttachmentRetentionEnabled = true,
            AttachmentRetentionDays = 0,
            AttachmentRetentionIntervalMinutes = 0
        });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("AttachmentRetentionDays", StringComparison.Ordinal));
        Assert.Contains(result.Failures!, failure => failure.Contains("AttachmentRetentionIntervalMinutes", StringComparison.Ordinal));
    }
}
