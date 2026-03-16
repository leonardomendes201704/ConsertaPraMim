using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyProviderMatchingOptionsValidator : IValidateOptions<JourneyProviderMatchingOptions>
{
    public ValidateOptionsResult Validate(string? name, JourneyProviderMatchingOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("JourneyProviderMatching ausente.");
        }

        if (options.WorkerIntervalSeconds < 5)
        {
            return ValidateOptionsResult.Fail("JourneyProviderMatching:WorkerIntervalSeconds deve ser >= 5.");
        }

        if (options.WorkerBatchSize <= 0)
        {
            return ValidateOptionsResult.Fail("JourneyProviderMatching:WorkerBatchSize deve ser > 0.");
        }

        if (options.MaxCandidatesToPersist <= 0)
        {
            return ValidateOptionsResult.Fail("JourneyProviderMatching:MaxCandidatesToPersist deve ser > 0.");
        }

        if (string.IsNullOrWhiteSpace(options.Timezone))
        {
            return ValidateOptionsResult.Fail("JourneyProviderMatching:Timezone e obrigatorio.");
        }

        return ValidateOptionsResult.Success;
    }
}
