using System.Linq;
using System.Text.RegularExpressions;

namespace AppMobileCPM.Integrations.Chatwoot;

internal static partial class ChatwootSecuritySanitizer
{
    public static string SanitizeMessage(string? value, int? maxLength = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var sanitized = value.Trim();
        sanitized = SensitiveKeyValueRegex().Replace(sanitized, match =>
        {
            var key = match.Groups["key"].Value;
            return $"{key}=[redacted]";
        });
        sanitized = BearerTokenRegex().Replace(sanitized, "Bearer [redacted]");
        sanitized = EmailRegex().Replace(sanitized, match => MaskEmail(match.Value));
        sanitized = PhoneRegex().Replace(sanitized, match => MaskPhone(match.Value));

        if (maxLength.HasValue && sanitized.Length > maxLength.Value)
        {
            sanitized = sanitized[..maxLength.Value];
        }

        return sanitized;
    }

    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        var trimmed = email.Trim();
        var atIndex = trimmed.IndexOf('@');
        if (atIndex <= 0 || atIndex >= trimmed.Length - 1)
        {
            return trimmed;
        }

        var localPart = trimmed[..atIndex];
        var domain = trimmed[(atIndex + 1)..];
        if (localPart.Length == 1)
        {
            return $"*@{domain}";
        }

        return $"{localPart[0]}***{localPart[^1]}@{domain}";
    }

    public static string MaskPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var digits = value.Where(char.IsDigit).ToArray();
        if (digits.Length < 8)
        {
            return value.Trim();
        }

        var visibleDigits = new string(digits.Skip(Math.Max(0, digits.Length - 4)).ToArray());
        var maskedDigits = new string('*', Math.Max(0, digits.Length - visibleDigits.Length)) + visibleDigits;
        var result = value.Trim().ToCharArray();
        var digitIndex = 0;

        for (var i = 0; i < result.Length; i++)
        {
            if (!char.IsDigit(result[i]))
            {
                continue;
            }

            result[i] = maskedDigits[digitIndex++];
        }

        return new string(result);
    }

    [GeneratedRegex(@"(?<key>(?i:api[_-]?access[_-]?token|authorization|webhook[_-]?secret|secret|token))\s*[:=]\s*(?:""[^""]*""|'[^']*'|[^\s,;]+)", RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveKeyValueRegex();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9._\-+=/]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"(?<![\w.+-])[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}(?![\w.\-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?<!\d)(?:\+?\d[\d\-\s().]{7,}\d)(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex PhoneRegex();
}
