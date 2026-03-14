using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Chatwoot;

public sealed class ChatwootOptionsValidator : IValidateOptions<ChatwootOptions>
{
    public ValidateOptionsResult Validate(string? name, ChatwootOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            failures.Add("Chatwoot:BaseUrl deve ser uma URL absoluta HTTP/HTTPS valida quando Chatwoot:Enabled=true.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiAccessToken))
        {
            failures.Add("Chatwoot:ApiAccessToken e obrigatorio quando Chatwoot:Enabled=true.");
        }

        if (options.AccountId <= 0)
        {
            failures.Add("Chatwoot:AccountId deve ser maior que zero quando Chatwoot:Enabled=true.");
        }

        if (options.ClientsInboxId <= 0)
        {
            failures.Add("Chatwoot:ClientsInboxId deve ser maior que zero quando Chatwoot:Enabled=true.");
        }

        if (options.ProvidersInboxId <= 0)
        {
            failures.Add("Chatwoot:ProvidersInboxId deve ser maior que zero quando Chatwoot:Enabled=true.");
        }

        if (string.IsNullOrWhiteSpace(options.WebhookSecret))
        {
            failures.Add("Chatwoot:WebhookSecret e obrigatorio quando Chatwoot:Enabled=true.");
        }

        if (options.RequestTimeoutSeconds <= 0)
        {
            failures.Add("Chatwoot:RequestTimeoutSeconds deve ser maior que zero.");
        }

        if (options.MaxRetryAttempts <= 0)
        {
            failures.Add("Chatwoot:MaxRetryAttempts deve ser maior que zero.");
        }

        if (options.RetryBaseDelayMs <= 0)
        {
            failures.Add("Chatwoot:RetryBaseDelayMs deve ser maior que zero.");
        }

        if (options.RetryWorkerIntervalSeconds <= 0)
        {
            failures.Add("Chatwoot:RetryWorkerIntervalSeconds deve ser maior que zero.");
        }

        if (options.RetryWorkerBatchSize <= 0)
        {
            failures.Add("Chatwoot:RetryWorkerBatchSize deve ser maior que zero.");
        }

        if (options.SyncQueueMaxAttempts <= 0)
        {
            failures.Add("Chatwoot:SyncQueueMaxAttempts deve ser maior que zero.");
        }

        if (options.WebhookPayloadRetentionDays <= 0)
        {
            failures.Add("Chatwoot:WebhookPayloadRetentionDays deve ser maior que zero.");
        }

        if (options.WebhookPayloadCleanupIntervalMinutes <= 0)
        {
            failures.Add("Chatwoot:WebhookPayloadCleanupIntervalMinutes deve ser maior que zero.");
        }

        foreach (var entry in options.GetAllowedWebhookIpEntries())
        {
            if (!ChatwootIpAllowlist.TryValidateEntry(entry, out _))
            {
                failures.Add($"Chatwoot:AllowedWebhookIps contem entrada invalida: '{entry}'.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
