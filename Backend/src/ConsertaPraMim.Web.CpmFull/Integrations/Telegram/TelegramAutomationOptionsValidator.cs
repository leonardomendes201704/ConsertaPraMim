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
