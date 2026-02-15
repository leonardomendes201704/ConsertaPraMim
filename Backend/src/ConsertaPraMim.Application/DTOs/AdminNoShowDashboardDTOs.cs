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
    IReadOnlyList<AdminNoShowRiskQueueItemDto> OpenRiskQueue);

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
