using System.Globalization;
using System.Text;

namespace AppMobileCPM.Integrations.Chatwoot;

public sealed class ChatwootLeadSourceMapping
{
    public required string DisplayName { get; init; }
    public required string Slug { get; init; }
    public required string RawValue { get; init; }
}

public static class ChatwootLeadSourceMappings
{
    public static ChatwootLeadSourceMapping? Resolve(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var rawValue = source.Trim();
        var normalizedLookup = NormalizeLookup(rawValue);

        if (ContainsAny(normalizedLookup, "whatsapp", "whats app", "whats", "zap"))
        {
            return Create("WhatsApp", "whatsapp", rawValue);
        }

        if (ContainsAny(normalizedLookup, "instagram", "insta"))
        {
            return Create("Instagram", "instagram", rawValue);
        }

        if (ContainsAny(normalizedLookup, "facebook", "face", "meta"))
        {
            return Create("Facebook", "facebook", rawValue);
        }

        if (ContainsAny(normalizedLookup, "tiktok", "tik tok"))
        {
            return Create("TikTok", "tiktok", rawValue);
        }

        if (ContainsAny(normalizedLookup, "telegram"))
        {
            return Create("Telegram", "telegram", rawValue);
        }

        if (ContainsAny(normalizedLookup, "email", "e mail", "mail"))
        {
            return Create("E-mail", "email", rawValue);
        }

        if (ContainsAny(normalizedLookup, "google", "ads", "adwords"))
        {
            return Create("Google", "google", rawValue);
        }

        if (ContainsAny(normalizedLookup, "site", "landing"))
        {
            return Create("Site", "site", rawValue);
        }

        if (ContainsAny(normalizedLookup, "indicacao", "referencia", "referral"))
        {
            return Create("Indicacao", "indicacao", rawValue);
        }

        if (ContainsAny(normalizedLookup, "manual"))
        {
            return Create("Manual", "manual", rawValue);
        }

        return Create(rawValue, BuildSlug(rawValue), rawValue);
    }

    private static ChatwootLeadSourceMapping Create(string displayName, string slug, string rawValue) =>
        new()
        {
            DisplayName = displayName,
            Slug = slug,
            RawValue = rawValue
        };

    private static bool ContainsAny(string haystack, params string[] needles) =>
        needles.Any(needle => haystack.Contains(needle, StringComparison.Ordinal));

    private static string NormalizeLookup(string value)
    {
        var withoutDiacritics = RemoveDiacritics(value).ToLowerInvariant();
        var chars = withoutDiacritics
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ')
            .ToArray();

        return string.Join(
            ' ',
            new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string BuildSlug(string value)
    {
        var withoutDiacritics = RemoveDiacritics(value).ToLowerInvariant();
        var chars = withoutDiacritics
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray();

        var collapsed = new string(chars);
        while (collapsed.Contains("__", StringComparison.Ordinal))
        {
            collapsed = collapsed.Replace("__", "_", StringComparison.Ordinal);
        }

        var slug = collapsed.Trim('_');
        return string.IsNullOrWhiteSpace(slug) ? "origem_nao_informada" : slug;
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }
}
