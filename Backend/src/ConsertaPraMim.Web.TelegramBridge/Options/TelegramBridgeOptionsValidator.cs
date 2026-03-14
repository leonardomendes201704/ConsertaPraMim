using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.TelegramBridge.Options;

public sealed class TelegramBridgeOptionsValidator : IValidateOptions<TelegramBridgeOptions>
{
    public ValidateOptionsResult Validate(string? name, TelegramBridgeOptions options)
    {
        var failures = new List<string>();

        if (options.PollingTimeoutSeconds <= 0)
        {
            failures.Add("TelegramBridge:PollingTimeoutSeconds deve ser maior que zero.");
        }

        if (options.IdleDelayMilliseconds <= 0)
        {
            failures.Add("TelegramBridge:IdleDelayMilliseconds deve ser maior que zero.");
        }

        if (options.MaxAttachmentBytes <= 0)
        {
            failures.Add("TelegramBridge:MaxAttachmentBytes deve ser maior que zero.");
        }

        if (options.MaxMessagesPerConversation <= 0)
        {
            failures.Add("TelegramBridge:MaxMessagesPerConversation deve ser maior que zero.");
        }

        if (options.AttachmentRetentionDays <= 0)
        {
            failures.Add("TelegramBridge:AttachmentRetentionDays deve ser maior que zero.");
        }

        if (options.AttachmentRetentionIntervalMinutes <= 0)
        {
            failures.Add("TelegramBridge:AttachmentRetentionIntervalMinutes deve ser maior que zero.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
