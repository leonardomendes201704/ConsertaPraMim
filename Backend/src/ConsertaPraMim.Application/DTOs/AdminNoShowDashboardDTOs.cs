namespace ConsertaPraMim.Application.DTOs;

public record AdminNoShowDashboardQueryDto(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? City = null,
    string? Category = null,
    string? RiskLevel = null,
    int QueueTake = 50,
    int CancellationNoShowWindowHours = 24);

public record AdminNoShowDashboardDto(
    DateTime FromUtc,
    DateTime ToUtc,
    string? CityFilter,
    string? CategoryFilter,
    string? RiskLevelFilter,
    int BaseAppointments,
    int NoShowAppointments,
    decimal NoShowRatePercent,
    int AttendanceAppointments,
    decimal AttendanceRatePercent,
    int DualPresenceConfirmedAppointments,
    decimal DualPresenceConfirmationRatePercent,
    int HighRiskAppointments,
    int HighRiskConvertedAppointments,
    decimal HighRiskConversionRatePercent,
    int OpenQueueItems,
    int HighRiskOpenQueueItems,
    double AverageQueueAgeMinutes,
    IReadOnlyList<AdminNoShowBreakdownDto> NoShowByCategory,
    IReadOnlyList<AdminNoShowBreakdownDto> NoShowByCity,
    IReadOnlyList<AdminNoShowRiskQueueItemDto> OpenRiskQueue,
    AdminNoShowRecurrenceSummaryDto RecurrenceSummary);

public record AdminNoShowBreakdownDto(
    string Name,
    int BaseAppointments,
    int NoShowAppointments,
    decimal NoShowRatePercent,
    int HighRiskAppointments);

public record AdminNoShowRiskQueueItemDto(
    Guid QueueItemId,
    Guid ServiceAppointmentId,
    Guid ServiceRequestId,
    string Category,
    string City,
    string ProviderName,
    string ClientName,
    string RiskLevel,
    int Score,
    string? Reasons,
    DateTime WindowStartUtc,
    DateTime LastDetectedAtUtc,
    DateTime FirstDetectedAtUtc);

public record AdminNoShowRecurrenceSummaryDto(
    DateTime WindowFromUtc,
    DateTime WindowToUtc,
    int LookbackDays,
    int ClientCriticalEvents,
    int ProviderCriticalEvents,
    int ClientsWithCriticalEvents,
    int ProvidersWithCriticalEvents,
    int RecurrentClients,
    int RecurrentProviders,
    decimal ClientRecurrentRatePercent,
    decimal ProviderRecurrentRatePercent,
    IReadOnlyList<AdminNoShowRecurrenceActorDto> TopRecurrentClients,
    IReadOnlyList<AdminNoShowRecurrenceActorDto> TopRecurrentProviders,
    IReadOnlyList<AdminNoShowRecurrenceTrendPointDto> DailyTrend);

public record AdminNoShowRecurrenceActorDto(
    Guid UserId,
    string UserName,
    int CriticalEvents,
    DateTime LastEventAtUtc,
    string LastEventType,
    string LastOutcome);

public record AdminNoShowRecurrenceTrendPointDto(
    DateTime DateUtc,
    int ClientCriticalEvents,
    int ProviderCriticalEvents,
    int TotalCriticalEvents);
