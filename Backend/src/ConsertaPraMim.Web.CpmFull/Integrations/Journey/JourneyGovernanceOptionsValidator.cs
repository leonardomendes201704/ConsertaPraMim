using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyGovernanceOptionsValidator : IValidateOptions<JourneyGovernanceOptions>
{
    public ValidateOptionsResult Validate(string? name, JourneyGovernanceOptions options)
    {
        if (options.RolloutPercentage < 0 || options.RolloutPercentage > 100)
        {
            return ValidateOptionsResult.Fail($"{JourneyGovernanceOptions.SectionName}:RolloutPercentage deve estar entre 0 e 100.");
        }

        return ValidateOptionsResult.Success;
    }
}
