using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Chatwoot;

public sealed class ChatwootConnectionHealthCheck : IHealthCheck
{
    private readonly IChatwootApiClient _chatwootApiClient;
    private readonly ChatwootOptions _options;

    public ChatwootConnectionHealthCheck(
        IChatwootApiClient chatwootApiClient,
        IOptions<ChatwootOptions> options)
    {
        _chatwootApiClient = chatwootApiClient;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return HealthCheckResult.Healthy("Integracao Chatwoot desabilitada.");
        }

        try
        {
            var result = await _chatwootApiClient.CheckConnectionAsync(cancellationToken);
            var inboxIds = result.Inboxes.Select(inbox => inbox.Id).ToHashSet();
            var missingInboxes = new List<string>();

            if (!inboxIds.Contains(_options.ClientsInboxId))
            {
                missingInboxes.Add($"ClientsInboxId={_options.ClientsInboxId}");
            }

            if (!inboxIds.Contains(_options.ProvidersInboxId))
            {
                missingInboxes.Add($"ProvidersInboxId={_options.ProvidersInboxId}");
            }

            if (missingInboxes.Count > 0)
            {
                return HealthCheckResult.Degraded(
                    $"Conectou no Chatwoot, mas os inboxes configurados nao foram encontrados: {string.Join(", ", missingInboxes)}.");
            }

            return HealthCheckResult.Healthy(
                $"Conectividade com Chatwoot validada. {result.Inboxes.Count} inbox(es) visiveis na conta.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Falha ao validar conectividade com o Chatwoot.", ex);
        }
    }
}
