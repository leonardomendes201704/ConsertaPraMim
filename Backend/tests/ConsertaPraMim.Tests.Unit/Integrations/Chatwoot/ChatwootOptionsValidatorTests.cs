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
            RetryBaseDelayMs = 500
        };

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.True(result.Succeeded);
    }
}
