using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyAutomationOptionsValidator : IValidateOptions<JourneyAutomationOptions>
{
    public ValidateOptionsResult Validate(string? name, JourneyAutomationOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.SharedSecret))
        {
            failures.Add("JourneyAutomation:SharedSecret e obrigatorio quando JourneyAutomation:Enabled=true.");
        }

        if (!options.ClientsAutomationEnabled && !options.ProvidersAutomationEnabled)
        {
            failures.Add("JourneyAutomation deve ter ao menos um funil habilitado entre clientes ou prestadores.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
