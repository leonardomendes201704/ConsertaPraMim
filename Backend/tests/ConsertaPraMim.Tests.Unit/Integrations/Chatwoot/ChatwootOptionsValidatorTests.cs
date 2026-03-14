using AppMobileCPM.Integrations.Chatwoot;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Tests.Unit.Integrations.Chatwoot;

public sealed class ChatwootOptionsValidatorTests
{
    private readonly ChatwootOptionsValidator _validator = new();

    [Fact(DisplayName = "Deve aceitar configuracao quando Chatwoot estiver desabilitado")]
    public void DeveAceitarConfiguracaoQuandoChatwootEstiverDesabilitado()
    {
        var options = new ChatwootOptions
        {
            Enabled = false
        };

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.True(result.Succeeded);
    }

    [Fact(DisplayName = "Deve falhar quando Chatwoot habilitado sem campos obrigatorios")]
    public void DeveFalharQuandoChatwootHabilitadoSemCamposObrigatorios()
    {
        var options = new ChatwootOptions
        {
            Enabled = true
        };

        var result = _validator.Validate(Options.DefaultName, options);
        var failures = result.Failures ?? [];

        Assert.False(result.Succeeded);
        Assert.Contains(failures, failure => failure.Contains("BaseUrl", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("ApiAccessToken", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("AccountId", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("ClientsInboxId", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("ProvidersInboxId", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("WebhookSecret", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Deve aceitar configuracao completa quando Chatwoot estiver habilitado")]
    public void DeveAceitarConfiguracaoCompletaQuandoChatwootEstiverHabilitado()
    {
        var options = new ChatwootOptions
        {
            Enabled = true,
            BaseUrl = "https://chat.exemplo.com",
            ApiAccessToken = "token-valido",
            AccountId = 1,
            ClientsInboxId = 10,
            ProvidersInboxId = 11,
            WebhookSecret = "segredo",
            RequestTimeoutSeconds = 15,
            MaxRetryAttempts = 3,
            RetryBaseDelayMs = 500,
            RetryWorkerEnabled = true,
            RetryWorkerIntervalSeconds = 30,
            RetryWorkerBatchSize = 20,
            SyncQueueMaxAttempts = 10
        };

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.True(result.Succeeded);
    }

    [Fact(DisplayName = "Deve falhar quando configuracao da fila Chatwoot for invalida")]
    public void DeveFalharQuandoConfiguracaoDaFilaChatwootForInvalida()
    {
        var options = new ChatwootOptions
        {
            Enabled = true,
            BaseUrl = "https://chat.exemplo.com",
            ApiAccessToken = "token-valido",
            AccountId = 1,
            ClientsInboxId = 10,
            ProvidersInboxId = 11,
            WebhookSecret = "segredo",
            RequestTimeoutSeconds = 15,
            MaxRetryAttempts = 3,
            RetryBaseDelayMs = 500,
            RetryWorkerIntervalSeconds = 0,
            RetryWorkerBatchSize = 0,
            SyncQueueMaxAttempts = 0
        };

        var result = _validator.Validate(Options.DefaultName, options);
        var failures = result.Failures ?? [];

        Assert.False(result.Succeeded);
        Assert.Contains(failures, failure => failure.Contains("RetryWorkerIntervalSeconds", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("RetryWorkerBatchSize", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("SyncQueueMaxAttempts", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Deve falhar quando configuracao de seguranca do webhook for invalida")]
    public void DeveFalharQuandoConfiguracaoDeSegurancaDoWebhookForInvalida()
    {
        var options = new ChatwootOptions
        {
            Enabled = true,
            BaseUrl = "https://chat.exemplo.com",
            ApiAccessToken = "token-valido",
            AccountId = 1,
            ClientsInboxId = 10,
            ProvidersInboxId = 11,
            WebhookSecret = "segredo",
            RequestTimeoutSeconds = 15,
            MaxRetryAttempts = 3,
            RetryBaseDelayMs = 500,
            RetryWorkerIntervalSeconds = 30,
            RetryWorkerBatchSize = 20,
            SyncQueueMaxAttempts = 10,
            AllowedWebhookIps = "10.0.0.0/33,ip-invalido",
            WebhookPayloadRetentionDays = 0,
            WebhookPayloadCleanupIntervalMinutes = 0
        };

        var result = _validator.Validate(Options.DefaultName, options);
        var failures = result.Failures ?? [];

        Assert.False(result.Succeeded);
        Assert.Contains(failures, failure => failure.Contains("AllowedWebhookIps", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("WebhookPayloadRetentionDays", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("WebhookPayloadCleanupIntervalMinutes", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Deve aceitar allowlist valida no webhook")]
    public void DeveAceitarAllowlistValidaNoWebhook()
    {
        var options = new ChatwootOptions
        {
            Enabled = true,
            BaseUrl = "https://chat.exemplo.com",
            ApiAccessToken = "token-valido",
            AccountId = 1,
            ClientsInboxId = 10,
            ProvidersInboxId = 11,
            WebhookSecret = "segredo",
            RequestTimeoutSeconds = 15,
            MaxRetryAttempts = 3,
            RetryBaseDelayMs = 500,
            RetryWorkerIntervalSeconds = 30,
            RetryWorkerBatchSize = 20,
            SyncQueueMaxAttempts = 10,
            AllowedWebhookIps = "127.0.0.1,10.0.0.0/24",
            WebhookPayloadRetentionDays = 14,
            WebhookPayloadCleanupIntervalMinutes = 360
        };

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.True(result.Succeeded);
    }
}
