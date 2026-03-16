using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyQualificationOptionsValidator : IValidateOptions<JourneyQualificationOptions>
{
    public ValidateOptionsResult Validate(string? name, JourneyQualificationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.RequestTimeoutSeconds is < 5 or > 90)
        {
            return ValidateOptionsResult.Fail("JourneyQualification:RequestTimeoutSeconds deve ficar entre 5 e 90 segundos.");
        }

        if (options.MaxRetries is < 0 or > 5)
        {
            return ValidateOptionsResult.Fail("JourneyQualification:MaxRetries deve ficar entre 0 e 5.");
        }

        if (options.MinimumConfidenceForAutoApply is < 0 or > 1)
        {
            return ValidateOptionsResult.Fail("JourneyQualification:MinimumConfidenceForAutoApply deve ficar entre 0 e 1.");
        }

        if (options.AiEnabled && string.IsNullOrWhiteSpace(options.OpenAiApiKey))
        {
            return ValidateOptionsResult.Fail("JourneyQualification:OpenAiApiKey e obrigatoria quando AiEnabled=true.");
        }

        if (options.AiEnabled && string.IsNullOrWhiteSpace(options.OpenAiModel))
        {
            return ValidateOptionsResult.Fail("JourneyQualification:OpenAiModel e obrigatorio quando AiEnabled=true.");
        }

        return ValidateOptionsResult.Success;
    }
}
