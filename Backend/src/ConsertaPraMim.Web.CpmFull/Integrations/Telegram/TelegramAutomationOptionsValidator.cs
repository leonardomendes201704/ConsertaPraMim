using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Telegram;

public sealed class TelegramAutomationOptionsValidator : IValidateOptions<TelegramAutomationOptions>
{
    public ValidateOptionsResult Validate(string? name, TelegramAutomationOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.SharedSecret))
        {
            failures.Add("TelegramAutomation:SharedSecret e obrigatorio quando TelegramAutomation:Enabled=true.");
        }

        if (options.MirrorMessagesEnabled)
        {
            if (!Uri.TryCreate(options.TelegramBridgeBaseUrl, UriKind.Absolute, out var bridgeUri) ||
                (bridgeUri.Scheme != Uri.UriSchemeHttp && bridgeUri.Scheme != Uri.UriSchemeHttps))
            {
                failures.Add("TelegramAutomation:TelegramBridgeBaseUrl deve ser uma URL absoluta HTTP/HTTPS valida quando TelegramAutomation:MirrorMessagesEnabled=true.");
            }

            if (options.RequestTimeoutSeconds <= 0)
            {
                failures.Add("TelegramAutomation:RequestTimeoutSeconds deve ser maior que zero.");
            }

            if (options.DeliveryWorkerIntervalSeconds <= 0)
            {
                failures.Add("TelegramAutomation:DeliveryWorkerIntervalSeconds deve ser maior que zero.");
            }

            if (options.DeliveryWorkerBatchSize <= 0)
            {
                failures.Add("TelegramAutomation:DeliveryWorkerBatchSize deve ser maior que zero.");
            }

            if (options.DeliveryQueueMaxAttempts <= 0)
            {
                failures.Add("TelegramAutomation:DeliveryQueueMaxAttempts deve ser maior que zero.");
            }
        }

        if (options.DeliveryPayloadRetentionDays <= 0)
        {
            failures.Add("TelegramAutomation:DeliveryPayloadRetentionDays deve ser maior que zero.");
        }

        if (options.DeliveryPayloadCleanupIntervalMinutes <= 0)
        {
            failures.Add("TelegramAutomation:DeliveryPayloadCleanupIntervalMinutes deve ser maior que zero.");
        }

        if (!options.ClientsAutomationEnabled &&
            !options.ProvidersAutomationEnabled &&
            !options.MirrorMessagesEnabled)
        {
            failures.Add("TelegramAutomation deve ter ao menos uma automacao habilitada entre clientes, prestadores ou espelhamento de mensagens.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
