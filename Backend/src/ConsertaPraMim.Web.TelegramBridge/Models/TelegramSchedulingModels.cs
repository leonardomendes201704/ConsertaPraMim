namespace ConsertaPraMim.Web.TelegramBridge.Models;

public sealed record TelegramSchedulingParseVisitWindow(
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    string DayLabel,
    string PeriodLabel);

public sealed record TelegramSchedulingParseResult(
    bool IsSchedulingIntent,
    int RequestedVisits,
    IReadOnlyList<TelegramSchedulingParseVisitWindow> Windows,
    string? ErrorCode = null,
    string? ErrorMessage = null);
