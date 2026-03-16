using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyProviderDispatchOptionsValidator : IValidateOptions<JourneyProviderDispatchOptions>
{
    public ValidateOptionsResult Validate(string? name, JourneyProviderDispatchOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("JourneyProviderDispatch ausente.");
        }

        if (options.WorkerIntervalSeconds < 5)
        {
            return ValidateOptionsResult.Fail("JourneyProviderDispatch:WorkerIntervalSeconds deve ser >= 5.");
        }

        if (options.WorkerBatchSize <= 0)
        {
            return ValidateOptionsResult.Fail("JourneyProviderDispatch:WorkerBatchSize deve ser > 0.");
        }

        if (options.QueueBatchSize <= 0)
        {
            return ValidateOptionsResult.Fail("JourneyProviderDispatch:QueueBatchSize deve ser > 0.");
        }

        if (options.WaveSize <= 0)
        {
            return ValidateOptionsResult.Fail("JourneyProviderDispatch:WaveSize deve ser > 0.");
        }

        if (options.MaxWaves <= 0)
        {
            return ValidateOptionsResult.Fail("JourneyProviderDispatch:MaxWaves deve ser > 0.");
        }

        if (options.AcceptanceTimeoutMinutes <= 0)
        {
            return ValidateOptionsResult.Fail("JourneyProviderDispatch:AcceptanceTimeoutMinutes deve ser > 0.");
        }

        if (options.QueueMaxAttempts <= 0)
        {
            return ValidateOptionsResult.Fail("JourneyProviderDispatch:QueueMaxAttempts deve ser > 0.");
        }

        if (string.IsNullOrWhiteSpace(options.DispatchStrategy))
        {
            return ValidateOptionsResult.Fail("JourneyProviderDispatch:DispatchStrategy e obrigatorio.");
        }

        return ValidateOptionsResult.Success;
    }
}
