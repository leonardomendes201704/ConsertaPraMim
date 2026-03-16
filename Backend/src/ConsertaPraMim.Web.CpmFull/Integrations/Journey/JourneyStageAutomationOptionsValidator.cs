using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyStageAutomationOptionsValidator : IValidateOptions<JourneyStageAutomationOptions>
{
    public ValidateOptionsResult Validate(string? name, JourneyStageAutomationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (options.WorkerIntervalSeconds is < 5 or > 300)
        {
            failures.Add($"{JourneyStageAutomationOptions.SectionName}:WorkerIntervalSeconds deve estar entre 5 e 300.");
        }

        if (options.WorkerBatchSize is < 1 or > 500)
        {
            failures.Add($"{JourneyStageAutomationOptions.SectionName}:WorkerBatchSize deve estar entre 1 e 500.");
        }

        if (options.PendingDataTimeoutMinutes is < 5 or > 4320)
        {
            failures.Add($"{JourneyStageAutomationOptions.SectionName}:PendingDataTimeoutMinutes deve estar entre 5 e 4320.");
        }

        if (options.ScheduleConfirmationTimeoutMinutes is < 5 or > 4320)
        {
            failures.Add($"{JourneyStageAutomationOptions.SectionName}:ScheduleConfirmationTimeoutMinutes deve estar entre 5 e 4320.");
        }

        if (options.ProviderAcceptanceTimeoutMinutes is < 5 or > 1440)
        {
            failures.Add($"{JourneyStageAutomationOptions.SectionName}:ProviderAcceptanceTimeoutMinutes deve estar entre 5 e 1440.");
        }

        if (options.ClientReviewTimeoutHours is < 1 or > 720)
        {
            failures.Add($"{JourneyStageAutomationOptions.SectionName}:ClientReviewTimeoutHours deve estar entre 1 e 720.");
        }

        if (options.ProviderReviewTimeoutHours is < 1 or > 720)
        {
            failures.Add($"{JourneyStageAutomationOptions.SectionName}:ProviderReviewTimeoutHours deve estar entre 1 e 720.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
