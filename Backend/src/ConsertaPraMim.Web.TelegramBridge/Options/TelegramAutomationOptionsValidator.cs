using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.TelegramBridge.Options;

public sealed class TelegramAutomationOptionsValidator : IValidateOptions<TelegramAutomationOptions>
{
    public ValidateOptionsResult Validate(string? name, TelegramAutomationOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (!Uri.TryCreate(options.CpmFullBaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            failures.Add("TelegramAutomation:CpmFullBaseUrl deve ser uma URL absoluta HTTP/HTTPS valida quando TelegramAutomation:Enabled=true.");
        }

        if (string.IsNullOrWhiteSpace(options.SharedSecret))
        {
            failures.Add("TelegramAutomation:SharedSecret e obrigatorio quando TelegramAutomation:Enabled=true.");
        }

        if (options.RequestTimeoutSeconds <= 0)
        {
            failures.Add("TelegramAutomation:RequestTimeoutSeconds deve ser maior que zero.");
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
