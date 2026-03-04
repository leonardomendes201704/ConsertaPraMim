using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Infrastructure.Configuration;

public sealed class GoogleCalendarSyncOptionsValidator : IValidateOptions<GoogleCalendarSyncOptions>
{
    public ValidateOptionsResult Validate(string? name, GoogleCalendarSyncOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

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
