using System.Globalization;
using System.Text;

namespace ConsertaPraMim.Domain.Enums;

public static class ServiceCategoryExtensions
{
    public static string ToPtBr(this ServiceCategory category)
    {
        return category switch
        {
            ServiceCategory.Electrical => "Eletrica",
            ServiceCategory.Plumbing => "Hidraulica",
            ServiceCategory.Electronics => "Eletronicos",
            ServiceCategory.Appliances => "Eletrodomesticos",
            ServiceCategory.Masonry => "Alvenaria",
            ServiceCategory.Cleaning => "Limpeza",
            ServiceCategory.Other => "Outros",
            _ => category.ToString()
        };
    }

    public static bool TryParseFlexible(string? value, out ServiceCategory category)
    {
        category = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (int.TryParse(trimmed, out var numeric) && Enum.IsDefined(typeof(ServiceCategory), numeric))
        {
            category = (ServiceCategory)numeric;
            return true;
        }

        if (Enum.TryParse(trimmed, true, out ServiceCategory parsed))
        {
            category = parsed;
            return true;
        }

        var normalized = NormalizeKey(trimmed);
        return normalized switch
        {
            "eletrica" => Assign(ServiceCategory.Electrical, out category),
            "hidraulica" => Assign(ServiceCategory.Plumbing, out category),
            "eletronicos" => Assign(ServiceCategory.Electronics, out category),
            "eletrodomesticos" => Assign(ServiceCategory.Appliances, out category),
            "alvenaria" => Assign(ServiceCategory.Masonry, out category),
            "limpeza" => Assign(ServiceCategory.Cleaning, out category),
            "outros" => Assign(ServiceCategory.Other, out category),
            "outras" => Assign(ServiceCategory.Other, out category),
            "outro" => Assign(ServiceCategory.Other, out category),
            _ => false
        };
    }

    public static string ToPtBrOrOriginal(string? value)
    {
        if (TryParseFlexible(value, out var parsed))
        {
            return parsed.ToPtBr();
        }

        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static bool Assign(ServiceCategory value, out ServiceCategory category)
    {
        category = value;
        return true;
    }

    private static string NormalizeKey(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString();
    }
}
