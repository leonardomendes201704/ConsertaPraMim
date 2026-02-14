using System.Globalization;
using System.Text;

namespace ConsertaPraMim.Domain.Enums;

public static class ServiceAppointmentOperationalStatusExtensions
{
    public static string ToPtBr(this ServiceAppointmentOperationalStatus status)
    {
        return status switch
        {
            ServiceAppointmentOperationalStatus.OnTheWay => "A caminho",
            ServiceAppointmentOperationalStatus.OnSite => "No local",
            ServiceAppointmentOperationalStatus.InService => "Em atendimento",
            ServiceAppointmentOperationalStatus.WaitingParts => "Aguardando peca",
            ServiceAppointmentOperationalStatus.Completed => "Concluido",
            _ => status.ToString()
        };
    }

    public static bool TryParseFlexible(string? value, out ServiceAppointmentOperationalStatus status)
    {
        status = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (int.TryParse(trimmed, out var numeric) && Enum.IsDefined(typeof(ServiceAppointmentOperationalStatus), numeric))
        {
            status = (ServiceAppointmentOperationalStatus)numeric;
            return true;
        }

        if (Enum.TryParse(trimmed, true, out ServiceAppointmentOperationalStatus parsed))
        {
            status = parsed;
            return true;
        }

        var normalized = NormalizeKey(trimmed);
        return normalized switch
        {
            "acaminho" => Assign(ServiceAppointmentOperationalStatus.OnTheWay, out status),
            "nolocal" => Assign(ServiceAppointmentOperationalStatus.OnSite, out status),
            "ematendimento" => Assign(ServiceAppointmentOperationalStatus.InService, out status),
            "aguardandopeca" => Assign(ServiceAppointmentOperationalStatus.WaitingParts, out status),
            "concluido" => Assign(ServiceAppointmentOperationalStatus.Completed, out status),
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

    private static bool Assign(ServiceAppointmentOperationalStatus value, out ServiceAppointmentOperationalStatus status)
    {
        status = value;
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
