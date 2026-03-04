using System.Text.RegularExpressions;
using ConsertaPraMim.Web.TelegramBridge.Models;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramSchedulingNaturalLanguageParser
{
    private static readonly Regex SchedulingIntentRegex = new(
        "\\b(agendar|agendamento|agenda|marcar|visita|visitas|horario|horarios|periodo)\\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex VisitCountNumberRegex = new(
        "\\b(?<count>[1-3])\\s*(?:prestador(?:es)?|visita(?:s)?)\\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex VisitCountWordRegex = new(
        "\\b(?<count>um|uma|dois|duas|tres)\\s*(?:prestador(?:es)?|visita(?:s)?)\\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DayRegex = new(
        "\\b(?<day>segunda|terca|quarta|quinta|sexta|sabado|domingo)(?:-feira)?\\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HourRegex = new(
        "\\b(?<start>\\d{1,2})\\s*h(?:\\s*(?:as|a|-)\\s*(?<end>\\d{1,2})\\s*h?)?\\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, DayOfWeek> DayMap = new Dictionary<string, DayOfWeek>(StringComparer.OrdinalIgnoreCase)
    {
        ["segunda"] = DayOfWeek.Monday,
        ["terca"] = DayOfWeek.Tuesday,
        ["quarta"] = DayOfWeek.Wednesday,
        ["quinta"] = DayOfWeek.Thursday,
        ["sexta"] = DayOfWeek.Friday,
        ["sabado"] = DayOfWeek.Saturday,
        ["domingo"] = DayOfWeek.Sunday
    };

    private static readonly IReadOnlyDictionary<string, int> CountWordMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["um"] = 1,
        ["uma"] = 1,
        ["dois"] = 2,
        ["duas"] = 2,
        ["tres"] = 3
    };

    private static readonly TimeZoneInfo SaoPauloTimeZone = ResolveSaoPauloTimeZone();

    public TelegramSchedulingParseResult Parse(
        string? clientMessage,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(clientMessage))
        {
            return new TelegramSchedulingParseResult(
                IsSchedulingIntent: false,
                RequestedVisits: 0,
                Windows: []);
        }

        var normalizedMessage = NormalizeText(clientMessage);
        var nowInSaoPaulo = ConvertUtcToSaoPaulo(nowUtc);
        var dayCandidates = ResolveRequestedDays(normalizedMessage, nowInSaoPaulo);
        var period = ResolvePeriod(normalizedMessage);

        var hasSchedulingKeyword = SchedulingIntentRegex.IsMatch(normalizedMessage);
        var hasSchedulingSignal = hasSchedulingKeyword || dayCandidates.Count > 0 || period is not null;
        if (!hasSchedulingSignal)
        {
            return new TelegramSchedulingParseResult(
                IsSchedulingIntent: false,
                RequestedVisits: 0,
                Windows: []);
        }

        if (period is null)
        {
            return new TelegramSchedulingParseResult(
                IsSchedulingIntent: true,
                RequestedVisits: 0,
                Windows: [],
                ErrorCode: "missing_period",
                ErrorMessage: "Informe o periodo/horario desejado para as visitas.");
        }

        if (dayCandidates.Count == 0)
        {
            return new TelegramSchedulingParseResult(
                IsSchedulingIntent: true,
                RequestedVisits: 0,
                Windows: [],
                ErrorCode: "missing_day",
                ErrorMessage: "Informe os dias desejados para o agendamento.");
        }

        var requestedVisits = ResolveRequestedVisits(normalizedMessage, dayCandidates.Count);
        if (requestedVisits > 3)
        {
            return new TelegramSchedulingParseResult(
                IsSchedulingIntent: true,
                RequestedVisits: requestedVisits,
                Windows: [],
                ErrorCode: "max_visits_exceeded",
                ErrorMessage: "O limite e de ate 3 visitas por pedido.");
        }

        if (requestedVisits > dayCandidates.Count)
        {
            return new TelegramSchedulingParseResult(
                IsSchedulingIntent: true,
                RequestedVisits: requestedVisits,
                Windows: [],
                ErrorCode: "insufficient_days",
                ErrorMessage: "Para cada visita, informe um dia diferente.");
        }

        var windows = BuildWindows(dayCandidates, requestedVisits, period.Value);
        return new TelegramSchedulingParseResult(
            IsSchedulingIntent: true,
            RequestedVisits: requestedVisits,
            Windows: windows);
    }

    private static DateTime ConvertUtcToSaoPaulo(DateTime nowUtc)
    {
        var utc = nowUtc.Kind == DateTimeKind.Utc
            ? nowUtc
            : nowUtc.ToUniversalTime();

        return TimeZoneInfo.ConvertTimeFromUtc(utc, SaoPauloTimeZone);
    }

    private static IReadOnlyList<TelegramSchedulingDayCandidate> ResolveRequestedDays(
        string normalizedMessage,
        DateTime nowInSaoPaulo)
    {
        var uniqueDates = new HashSet<DateOnly>();
        var candidates = new List<TelegramSchedulingDayCandidate>();
        var useNextWeek = ShouldUseNextWeek(normalizedMessage);

        foreach (Match match in DayRegex.Matches(normalizedMessage))
        {
            var dayToken = match.Groups["day"].Value;
            if (!DayMap.TryGetValue(dayToken, out var dayOfWeek))
            {
                continue;
            }

            var localDate = ResolveNextDate(nowInSaoPaulo, dayOfWeek, useNextWeek);
            if (!uniqueDates.Add(localDate))
            {
                continue;
            }

            candidates.Add(new TelegramSchedulingDayCandidate(
                Index: match.Index,
                Date: localDate,
                Label: dayToken));
        }

        if (normalizedMessage.Contains("amanha", StringComparison.Ordinal))
        {
            var tomorrow = DateOnly.FromDateTime(nowInSaoPaulo.Date.AddDays(1));
            if (uniqueDates.Add(tomorrow))
            {
                candidates.Add(new TelegramSchedulingDayCandidate(
                    Index: normalizedMessage.IndexOf("amanha", StringComparison.Ordinal),
                    Date: tomorrow,
                    Label: "amanha"));
            }
        }

        return candidates
            .OrderBy(item => item.Index)
            .ToList();
    }

    private static int ResolveRequestedVisits(string normalizedMessage, int dayCount)
    {
        var numberMatch = VisitCountNumberRegex.Match(normalizedMessage);
        if (numberMatch.Success && int.TryParse(numberMatch.Groups["count"].Value, out var numericCount))
        {
            return Math.Clamp(numericCount, 1, 3);
        }

        var wordMatch = VisitCountWordRegex.Match(normalizedMessage);
        if (wordMatch.Success &&
            CountWordMap.TryGetValue(wordMatch.Groups["count"].Value, out var wordCount))
        {
            return Math.Clamp(wordCount, 1, 3);
        }

        return Math.Clamp(dayCount, 1, 3);
    }

    private static IReadOnlyList<TelegramSchedulingParseVisitWindow> BuildWindows(
        IReadOnlyList<TelegramSchedulingDayCandidate> dayCandidates,
        int requestedVisits,
        TelegramSchedulingPeriod period)
    {
        var windows = new List<TelegramSchedulingParseVisitWindow>(requestedVisits);
        foreach (var day in dayCandidates.Take(requestedVisits))
        {
            var localStart = day.Date.ToDateTime(period.Start);
            var localEnd = day.Date.ToDateTime(period.End);

            var utcStart = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(localStart, DateTimeKind.Unspecified),
                SaoPauloTimeZone);

            var utcEnd = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(localEnd, DateTimeKind.Unspecified),
                SaoPauloTimeZone);

            windows.Add(new TelegramSchedulingParseVisitWindow(
                WindowStartUtc: utcStart,
                WindowEndUtc: utcEnd,
                DayLabel: day.Label,
                PeriodLabel: period.Label));
        }

        return windows;
    }

    private static DateOnly ResolveNextDate(DateTime nowInSaoPaulo, DayOfWeek target)
    {
        var daysAhead = ((int)target - (int)nowInSaoPaulo.DayOfWeek + 7) % 7;
        if (daysAhead == 0)
        {
            daysAhead = 7;
        }

        return DateOnly.FromDateTime(nowInSaoPaulo.Date.AddDays(daysAhead));
    }

    private static DateOnly ResolveNextDate(
        DateTime nowInSaoPaulo,
        DayOfWeek target,
        bool useNextWeek)
    {
        var daysAhead = ((int)target - (int)nowInSaoPaulo.DayOfWeek + 7) % 7;
        if (daysAhead == 0)
        {
            daysAhead = 7;
        }

        if (useNextWeek && daysAhead < 7)
        {
            daysAhead += 7;
        }

        return DateOnly.FromDateTime(nowInSaoPaulo.Date.AddDays(daysAhead));
    }

    private static bool ShouldUseNextWeek(string normalizedMessage)
    {
        return normalizedMessage.Contains("semana que vem", StringComparison.Ordinal)
               || normalizedMessage.Contains("semana q vem", StringComparison.Ordinal)
               || normalizedMessage.Contains("proxima semana", StringComparison.Ordinal);
    }

    private static TelegramSchedulingPeriod? ResolvePeriod(string normalizedMessage)
    {
        var hourMatch = HourRegex.Match(normalizedMessage);
        if (hourMatch.Success)
        {
            var startHour = ParseHour(hourMatch.Groups["start"].Value);
            if (!startHour.HasValue)
            {
                return null;
            }

            var endHour = ParseHour(hourMatch.Groups["end"].Value)
                          ?? Math.Min(startHour.Value + 2, 23);

            if (endHour <= startHour)
            {
                endHour = Math.Min(startHour.Value + 2, 23);
            }

            return new TelegramSchedulingPeriod(
                Start: new TimeOnly(startHour.Value, 0),
                End: new TimeOnly(endHour, 0),
                Label: "horario_informado");
        }

        if (normalizedMessage.Contains("manha", StringComparison.Ordinal))
        {
            return new TelegramSchedulingPeriod(
                Start: new TimeOnly(9, 0),
                End: new TimeOnly(11, 0),
                Label: "manha");
        }

        if (normalizedMessage.Contains("tarde", StringComparison.Ordinal))
        {
            return new TelegramSchedulingPeriod(
                Start: new TimeOnly(14, 0),
                End: new TimeOnly(16, 0),
                Label: "tarde");
        }

        if (normalizedMessage.Contains("noite", StringComparison.Ordinal))
        {
            return new TelegramSchedulingPeriod(
                Start: new TimeOnly(19, 0),
                End: new TimeOnly(21, 0),
                Label: "noite");
        }

        return null;
    }

    private static int? ParseHour(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (!int.TryParse(raw, out var value))
        {
            return null;
        }

        return value is >= 0 and <= 23
            ? value
            : null;
    }

    private static string NormalizeText(string value)
    {
        return value.Trim()
            .ToLowerInvariant()
            .Replace("á", "a", StringComparison.Ordinal)
            .Replace("à", "a", StringComparison.Ordinal)
            .Replace("ã", "a", StringComparison.Ordinal)
            .Replace("â", "a", StringComparison.Ordinal)
            .Replace("é", "e", StringComparison.Ordinal)
            .Replace("ê", "e", StringComparison.Ordinal)
            .Replace("í", "i", StringComparison.Ordinal)
            .Replace("ó", "o", StringComparison.Ordinal)
            .Replace("ô", "o", StringComparison.Ordinal)
            .Replace("õ", "o", StringComparison.Ordinal)
            .Replace("ú", "u", StringComparison.Ordinal)
            .Replace("ç", "c", StringComparison.Ordinal);
    }

    private static TimeZoneInfo ResolveSaoPauloTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Utc;
            }
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private readonly record struct TelegramSchedulingPeriod(
        TimeOnly Start,
        TimeOnly End,
        string Label);

    private readonly record struct TelegramSchedulingDayCandidate(
        int Index,
        DateOnly Date,
        string Label);
}
