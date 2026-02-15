using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Repositories;

public interface IAdminNoShowDashboardRepository
{
    Task<AdminNoShowDashboardKpiReadModel> GetKpisAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? cityFilter,
        string? categoryFilter,
        ServiceAppointmentNoShowRiskLevel? riskLevelFilter,
        int cancellationNoShowWindowHours = 24);

    Task<IReadOnlyList<AdminNoShowBreakdownReadModel>> GetBreakdownByCategoryAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? cityFilter,
        ServiceAppointmentNoShowRiskLevel? riskLevelFilter,
        int cancellationNoShowWindowHours = 24);

    Task<IReadOnlyList<AdminNoShowBreakdownReadModel>> GetBreakdownByCityAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? categoryFilter,
        ServiceAppointmentNoShowRiskLevel? riskLevelFilter,
        int cancellationNoShowWindowHours = 24);

    Task<IReadOnlyList<AdminNoShowRiskQueueItemReadModel>> GetOpenRiskQueueAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? cityFilter,
        string? categoryFilter,
        ServiceAppointmentNoShowRiskLevel? riskLevelFilter,
        int take = 100);
}

public record AdminNoShowDashboardKpiReadModel(
    int BaseAppointments,
    int NoShowAppointments,
    int AttendanceAppointments,
    int DualPresenceConfirmedAppointments,
    int HighRiskAppointments,
    int HighRiskConvertedAppointments,
    int OpenQueueItems,
    int HighRiskOpenQueueItems,
    double AverageQueueAgeMinutes);

public record AdminNoShowBreakdownReadModel(
    string Name,
    int BaseAppointments,
    int NoShowAppointments,
    int HighRiskAppointments);

public record AdminNoShowRiskQueueItemReadModel(
    Guid QueueItemId,
    Guid ServiceAppointmentId,
    Guid ServiceRequestId,
    string Category,
    string City,
    string ProviderName,
    string ClientName,
    ServiceAppointmentNoShowRiskLevel RiskLevel,
    int Score,
    string? ReasonsCsv,
    DateTime WindowStartUtc,
    DateTime LastDetectedAtUtc,
    DateTime FirstDetectedAtUtc);
