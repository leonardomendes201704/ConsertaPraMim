using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Infrastructure.Configuration;

public sealed class GoogleCalendarSyncOptionsValidator : IValidateOptions<GoogleCalendarSyncOptions>
{
    public ValidateOptionsResult Validate(string? name, GoogleCalendarSyncOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        ValidateRetrySettings(options, failures);

        if (!options.Enabled)
        {
            return failures.Count == 0
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(failures);
        }

        if (string.IsNullOrWhiteSpace(options.ProjectId))
        {
            failures.Add($"{GoogleCalendarSyncOptions.SectionName}:ProjectId obrigatorio quando Enabled=true.");
        }

        if (string.IsNullOrWhiteSpace(options.ServiceAccountEmail))
        {
            failures.Add($"{GoogleCalendarSyncOptions.SectionName}:ServiceAccountEmail obrigatorio quando Enabled=true.");
        }

        if (string.IsNullOrWhiteSpace(options.PrivateKey))
        {
            failures.Add($"{GoogleCalendarSyncOptions.SectionName}:PrivateKey obrigatorio quando Enabled=true.");
        }

        if (string.IsNullOrWhiteSpace(options.CalendarId))
        {
            failures.Add($"{GoogleCalendarSyncOptions.SectionName}:CalendarId obrigatorio quando Enabled=true.");
        }

        if (string.IsNullOrWhiteSpace(options.Timezone))
        {
            failures.Add($"{GoogleCalendarSyncOptions.SectionName}:Timezone obrigatorio quando Enabled=true.");
        }
        else if (!CanResolveTimeZone(options.Timezone))
        {
            failures.Add($"{GoogleCalendarSyncOptions.SectionName}:Timezone invalido ({options.Timezone}).");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateRetrySettings(
        GoogleCalendarSyncOptions options,
        List<string> failures)
    {
        if (options.RetryMaxAttempts is < 1 or > 20)
        {
            failures.Add($"{GoogleCalendarSyncOptions.SectionName}:RetryMaxAttempts deve estar entre 1 e 20.");
        }

        if (options.RetryBaseDelaySeconds is < 1 or > 3600)
        {
            failures.Add($"{GoogleCalendarSyncOptions.SectionName}:RetryBaseDelaySeconds deve estar entre 1 e 3600.");
        }

        if (options.RetryMaxDelaySeconds is < 1 or > 21600)
        {
            failures.Add($"{GoogleCalendarSyncOptions.SectionName}:RetryMaxDelaySeconds deve estar entre 1 e 21600.");
        }

        if (options.RetryMaxDelaySeconds < options.RetryBaseDelaySeconds)
        {
            failures.Add($"{GoogleCalendarSyncOptions.SectionName}:RetryMaxDelaySeconds nao pode ser menor que RetryBaseDelaySeconds.");
        }

        if (options.RetryJitterMaxSeconds is < 0 or > 300)
        {
            failures.Add($"{GoogleCalendarSyncOptions.SectionName}:RetryJitterMaxSeconds deve estar entre 0 e 300.");
        }

        if (options.RetryWorkerIntervalSeconds is < 5 or > 3600)
        {
            failures.Add($"{GoogleCalendarSyncOptions.SectionName}:RetryWorkerIntervalSeconds deve estar entre 5 e 3600.");
        }

        if (options.RetryWorkerBatchSize is < 1 or > 1000)
        {
            failures.Add($"{GoogleCalendarSyncOptions.SectionName}:RetryWorkerBatchSize deve estar entre 1 e 1000.");
        }
    }

    private static bool CanResolveTimeZone(string timeZoneId)
    {
        var normalized = timeZoneId.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (TryFindTimeZone(normalized) != null)
        {
            return true;
        }

        return normalized.Equals("America/Sao_Paulo", StringComparison.OrdinalIgnoreCase) &&
               TryFindTimeZone("E. South America Standard Time") != null;
    }

    private static TimeZoneInfo? TryFindTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }
}
