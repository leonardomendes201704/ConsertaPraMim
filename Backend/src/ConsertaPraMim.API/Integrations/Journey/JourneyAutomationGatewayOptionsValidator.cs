using Microsoft.Extensions.Options;

namespace ConsertaPraMim.API.Integrations.Journey;

public sealed class JourneyAutomationGatewayOptionsValidator : IValidateOptions<JourneyAutomationGatewayOptions>
{
    public ValidateOptionsResult Validate(string? name, JourneyAutomationGatewayOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.SharedSecret))
        {
            failures.Add("JourneyAutomationGateway:SharedSecret e obrigatorio quando JourneyAutomationGateway:Enabled=true.");
        }

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            failures.Add("JourneyAutomationGateway:BaseUrl deve ser uma URL absoluta HTTP/HTTPS valida quando a automacao estiver habilitada.");
        }

        if (options.RequestTimeoutSeconds <= 0)
        {
            failures.Add("JourneyAutomationGateway:RequestTimeoutSeconds deve ser maior que zero.");
        }

        if (!options.ClientsAutomationEnabled && !options.ProvidersAutomationEnabled)
        {
            failures.Add("JourneyAutomationGateway deve ter ao menos um funil habilitado entre clientes ou prestadores.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
