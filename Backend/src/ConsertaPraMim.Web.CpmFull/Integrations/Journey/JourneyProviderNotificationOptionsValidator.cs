using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyProviderNotificationOptionsValidator : IValidateOptions<JourneyProviderNotificationOptions>
{
    public ValidateOptionsResult Validate(string? name, JourneyProviderNotificationOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("JourneyProviderNotification ausente.");
        }

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.PublicBaseUrl) ||
            !Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out _))
        {
            return ValidateOptionsResult.Fail("JourneyProviderNotification:PublicBaseUrl deve ser uma URL absoluta.");
        }

        if (string.IsNullOrWhiteSpace(options.LinkSigningSecret) || options.LinkSigningSecret.Trim().Length < 32)
        {
            return ValidateOptionsResult.Fail("JourneyProviderNotification:LinkSigningSecret deve ter ao menos 32 caracteres.");
        }

        if (options.LinkExpirationMinutes <= 0)
        {
            return ValidateOptionsResult.Fail("JourneyProviderNotification:LinkExpirationMinutes deve ser > 0.");
        }

        if (!options.EmailEnabled)
        {
            return ValidateOptionsResult.Success;
        }

        var normalizedTransport = NormalizeTransport(options.EmailTransport);
        if (normalizedTransport is not ("log" or "smtp"))
        {
            return ValidateOptionsResult.Fail("JourneyProviderNotification:EmailTransport deve ser 'log' ou 'smtp'.");
        }

        if (string.IsNullOrWhiteSpace(options.SenderEmail))
        {
            return ValidateOptionsResult.Fail("JourneyProviderNotification:SenderEmail e obrigatorio quando EmailEnabled=true.");
        }

        if (normalizedTransport == "smtp")
        {
            if (string.IsNullOrWhiteSpace(options.SmtpHost))
            {
                return ValidateOptionsResult.Fail("JourneyProviderNotification:SmtpHost e obrigatorio quando EmailTransport=smtp.");
            }

            if (options.SmtpPort <= 0)
            {
                return ValidateOptionsResult.Fail("JourneyProviderNotification:SmtpPort deve ser > 0 quando EmailTransport=smtp.");
            }

            if (string.IsNullOrWhiteSpace(options.SmtpUsername))
            {
                return ValidateOptionsResult.Fail("JourneyProviderNotification:SmtpUsername e obrigatorio quando EmailTransport=smtp.");
            }

            if (string.IsNullOrWhiteSpace(options.SmtpPassword))
            {
                return ValidateOptionsResult.Fail("JourneyProviderNotification:SmtpPassword e obrigatorio quando EmailTransport=smtp.");
            }
        }

        if (!string.IsNullOrWhiteSpace(options.ProviderPortalBaseUrl) &&
            !Uri.TryCreate(options.ProviderPortalBaseUrl, UriKind.Absolute, out _))
        {
            return ValidateOptionsResult.Fail("JourneyProviderNotification:ProviderPortalBaseUrl deve ser uma URL absoluta.");
        }

        return ValidateOptionsResult.Success;
    }

    private static string NormalizeTransport(string? transport) => (transport ?? string.Empty).Trim().ToLowerInvariant();
}
