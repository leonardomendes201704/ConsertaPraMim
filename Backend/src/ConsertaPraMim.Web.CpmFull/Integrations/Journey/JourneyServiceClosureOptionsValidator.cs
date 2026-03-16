using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyServiceClosureOptionsValidator : IValidateOptions<JourneyServiceClosureOptions>
{
    public ValidateOptionsResult Validate(string? name, JourneyServiceClosureOptions options)
    {
        if (options.CompletionLinkExpirationHours is < 1 or > 720)
        {
            return ValidateOptionsResult.Fail($"{JourneyServiceClosureOptions.SectionName}:CompletionLinkExpirationHours deve estar entre 1 e 720.");
        }

        if (options.ReviewLinkExpirationHours is < 1 or > 720)
        {
            return ValidateOptionsResult.Fail($"{JourneyServiceClosureOptions.SectionName}:ReviewLinkExpirationHours deve estar entre 1 e 720.");
        }

        if (options.LowScoreThreshold is < 1 or > 5)
        {
            return ValidateOptionsResult.Fail($"{JourneyServiceClosureOptions.SectionName}:LowScoreThreshold deve estar entre 1 e 5.");
        }

        return ValidateOptionsResult.Success;
    }
}
