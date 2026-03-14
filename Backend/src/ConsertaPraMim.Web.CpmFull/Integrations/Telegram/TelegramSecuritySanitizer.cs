using System.Linq;
using System.Text.RegularExpressions;

namespace AppMobileCPM.Integrations.Telegram;

internal static partial class TelegramSecuritySanitizer
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
        sanitized = ChatIdKeyValueRegex().Replace(sanitized, match =>
        {
            var key = match.Groups["key"].Value;
            var valueGroup = match.Groups["value"].Value;
            return $"{key}={MaskChatId(valueGroup)}";
        });

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

        return MaskDigits(value.Trim(), minimumDigits: 8, visibleDigits: 4);
    }

    public static string MaskChatId(long? chatId)
    {
        return chatId.HasValue
            ? MaskChatId(chatId.Value.ToString())
            : string.Empty;
    }

    public static string MaskChatId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return MaskDigits(value.Trim(), minimumDigits: 6, visibleDigits: 4);
    }

    private static string MaskDigits(string value, int minimumDigits, int visibleDigits)
    {
        var digits = value.Where(char.IsDigit).ToArray();
        if (digits.Length < minimumDigits)
        {
            return value;
        }

        var visible = new string(digits.Skip(Math.Max(0, digits.Length - visibleDigits)).ToArray());
        var maskedDigits = new string('*', Math.Max(0, digits.Length - visible.Length)) + visible;
        var characters = value.ToCharArray();
        var digitIndex = 0;

        for (var i = 0; i < characters.Length; i++)
        {
            if (!char.IsDigit(characters[i]))
            {
                continue;
            }

            characters[i] = maskedDigits[digitIndex++];
        }

        return new string(characters);
    }

    [GeneratedRegex(@"(?<key>(?i:api[_-]?key|api[_-]?access[_-]?token|authorization|bot[_-]?token|dashboard[_-]?token|shared[_-]?secret|webhook[_-]?secret|secret|token))\s*[:=]\s*(?:""[^""]*""|'[^']*'|[^\s,;]+)", RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveKeyValueRegex();

    [GeneratedRegex(@"(?<key>(?i:chat[_-]?id|telegram[_-]?chat[_-]?id|telegramchatid))\s*[:=]\s*(?<value>\+?\d[\d\-\s().]{4,}\d)", RegexOptions.CultureInvariant)]
    private static partial Regex ChatIdKeyValueRegex();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9._\-+=/]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"(?<![\w.+-])[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}(?![\w.\-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?<!\d)(?:\+?\d[\d\-\s().]{7,}\d)(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex PhoneRegex();
}
