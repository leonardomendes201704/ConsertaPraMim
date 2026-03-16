using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneySchedulingOptionsValidator : IValidateOptions<JourneySchedulingOptions>
{
    public ValidateOptionsResult Validate(string? name, JourneySchedulingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.ProjectId))
        {
            failures.Add($"{JourneySchedulingOptions.SectionName}:ProjectId obrigatorio quando Enabled=true.");
        }

        if (string.IsNullOrWhiteSpace(options.ServiceAccountEmail))
        {
            failures.Add($"{JourneySchedulingOptions.SectionName}:ServiceAccountEmail obrigatorio quando Enabled=true.");
        }

        if (string.IsNullOrWhiteSpace(options.PrivateKey))
        {
            failures.Add($"{JourneySchedulingOptions.SectionName}:PrivateKey obrigatorio quando Enabled=true.");
        }

        if (string.IsNullOrWhiteSpace(options.CalendarId))
        {
            failures.Add($"{JourneySchedulingOptions.SectionName}:CalendarId obrigatorio quando Enabled=true.");
        }

        if (string.IsNullOrWhiteSpace(options.Timezone))
        {
            failures.Add($"{JourneySchedulingOptions.SectionName}:Timezone obrigatorio quando Enabled=true.");
        }

        if (!TimeOnly.TryParse(options.BusinessHoursStartLocal, out var startLocal))
        {
            failures.Add($"{JourneySchedulingOptions.SectionName}:BusinessHoursStartLocal invalido.");
        }

        if (!TimeOnly.TryParse(options.BusinessHoursEndLocal, out var endLocal))
        {
            failures.Add($"{JourneySchedulingOptions.SectionName}:BusinessHoursEndLocal invalido.");
        }

        if (TimeOnly.TryParse(options.BusinessHoursStartLocal, out startLocal) &&
            TimeOnly.TryParse(options.BusinessHoursEndLocal, out endLocal) &&
            endLocal <= startLocal)
        {
            failures.Add($"{JourneySchedulingOptions.SectionName}:BusinessHoursEndLocal deve ser maior que BusinessHoursStartLocal.");
        }

        if (options.SlotDurationMinutes is < 30 or > 480)
        {
            failures.Add($"{JourneySchedulingOptions.SectionName}:SlotDurationMinutes deve estar entre 30 e 480.");
        }

        if (options.SuggestionCount is < 1 or > 8)
        {
            failures.Add($"{JourneySchedulingOptions.SectionName}:SuggestionCount deve estar entre 1 e 8.");
        }

        if (options.SuggestionWindowDays is < 1 or > 30)
        {
            failures.Add($"{JourneySchedulingOptions.SectionName}:SuggestionWindowDays deve estar entre 1 e 30.");
        }

        if (options.MinimumNoticeMinutes is < 0 or > 1440)
        {
            failures.Add($"{JourneySchedulingOptions.SectionName}:MinimumNoticeMinutes deve estar entre 0 e 1440.");
        }

        if (options.RequestTimeoutSeconds is < 5 or > 120)
        {
            failures.Add($"{JourneySchedulingOptions.SectionName}:RequestTimeoutSeconds deve estar entre 5 e 120.");
        }

        if (options.TokenRefreshSafetyMinutes is < 1 or > 30)
        {
            failures.Add($"{JourneySchedulingOptions.SectionName}:TokenRefreshSafetyMinutes deve estar entre 1 e 30.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
