using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace ConsertaPraMim.Web.TelegramBridge.Options;

public sealed class TelegramBridgeOptionsValidator : IValidateOptions<TelegramBridgeOptions>
{
    private static readonly Regex WebhookSecretTokenPattern = new("^[A-Za-z0-9_-]{1,256}$", RegexOptions.Compiled);

    public ValidateOptionsResult Validate(string? name, TelegramBridgeOptions options)
    {
        var failures = new List<string>();

        var normalizedTransport = options.UpdateTransport?.Trim();
        if (!string.Equals(normalizedTransport, TelegramBridgeOptions.LongPollingTransport, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalizedTransport, TelegramBridgeOptions.WebhookTransport, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("TelegramBridge:UpdateTransport deve ser LongPolling ou Webhook.");
        }

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

        if (options.UsesWebhookTransport())
        {
            if (!Uri.TryCreate(options.WebhookPublicBaseUrl?.Trim(), UriKind.Absolute, out var webhookBaseUri))
            {
                failures.Add("TelegramBridge:WebhookPublicBaseUrl deve ser uma URL absoluta valida no modo Webhook.");
            }
            else if (!string.Equals(webhookBaseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add("TelegramBridge:WebhookPublicBaseUrl deve usar HTTPS no modo Webhook.");
            }

            if (string.IsNullOrWhiteSpace(options.WebhookPath))
            {
                failures.Add("TelegramBridge:WebhookPath deve ser informado no modo Webhook.");
            }
            else if (!options.WebhookPath.Trim().StartsWith("/", StringComparison.Ordinal))
            {
                failures.Add("TelegramBridge:WebhookPath deve comecar com '/'.");
            }

            if (string.IsNullOrWhiteSpace(options.WebhookSecretToken))
            {
                failures.Add("TelegramBridge:WebhookSecretToken deve ser informado no modo Webhook.");
            }
            else if (!WebhookSecretTokenPattern.IsMatch(options.WebhookSecretToken.Trim()))
            {
                failures.Add("TelegramBridge:WebhookSecretToken deve conter apenas letras, numeros, '_' ou '-'.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
