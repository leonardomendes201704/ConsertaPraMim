using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class AdminNoShowDashboardService : IAdminNoShowDashboardService
{
    private readonly IAdminNoShowDashboardRepository _repository;

    public AdminNoShowDashboardService(IAdminNoShowDashboardRepository repository)
    {
        _repository = repository;
    }

    public async Task<AdminNoShowDashboardDto> GetDashboardAsync(AdminNoShowDashboardQueryDto query)
    {
        var (fromUtc, toUtc) = NormalizeDateRange(query.FromUtc, query.ToUtc);
        var riskLevelFilter = ParseRiskLevel(query.RiskLevel);
        var queueTake = Math.Clamp(query.QueueTake, 1, 500);
        var cancellationNoShowWindowHours = Math.Clamp(query.CancellationNoShowWindowHours, 1, 168);

        var kpis = await _repository.GetKpisAsync(
            fromUtc,
            toUtc,
            query.City,
            query.Category,
            riskLevelFilter,
            cancellationNoShowWindowHours);

        var noShowByCategory = await _repository.GetBreakdownByCategoryAsync(
            fromUtc,
            toUtc,
            query.City,
            riskLevelFilter,
            cancellationNoShowWindowHours);

        var noShowByCity = await _repository.GetBreakdownByCityAsync(
            fromUtc,
            toUtc,
            query.Category,
            riskLevelFilter,
            cancellationNoShowWindowHours);

        var queue = await _repository.GetOpenRiskQueueAsync(
            fromUtc,
            toUtc,
            query.City,
            query.Category,
            riskLevelFilter,
            queueTake);

        var baseAppointments = kpis.BaseAppointments;
        var noShowRatePercent = CalculateRate(kpis.NoShowAppointments, baseAppointments);
        var attendanceRatePercent = CalculateRate(kpis.AttendanceAppointments, baseAppointments);
        var dualPresenceRatePercent = CalculateRate(kpis.DualPresenceConfirmedAppointments, baseAppointments);
        var highRiskConversionRatePercent = CalculateRate(kpis.HighRiskConvertedAppointments, kpis.HighRiskAppointments);

        return new AdminNoShowDashboardDto(
            fromUtc,
            toUtc,
            query.City,
            query.Category,
            riskLevelFilter?.ToString(),
            baseAppointments,
            kpis.NoShowAppointments,
            noShowRatePercent,
            kpis.AttendanceAppointments,
            attendanceRatePercent,
            kpis.DualPresenceConfirmedAppointments,
            dualPresenceRatePercent,
            kpis.HighRiskAppointments,
            kpis.HighRiskConvertedAppointments,
            highRiskConversionRatePercent,
            kpis.OpenQueueItems,
            kpis.HighRiskOpenQueueItems,
            Math.Round(kpis.AverageQueueAgeMinutes, 1, MidpointRounding.AwayFromZero),
            noShowByCategory.Select(item => new AdminNoShowBreakdownDto(
                item.Name,
                item.BaseAppointments,
                item.NoShowAppointments,
                CalculateRate(item.NoShowAppointments, item.BaseAppointments),
                item.HighRiskAppointments)).ToList(),
            noShowByCity.Select(item => new AdminNoShowBreakdownDto(
                item.Name,
                item.BaseAppointments,
                item.NoShowAppointments,
                CalculateRate(item.NoShowAppointments, item.BaseAppointments),
                item.HighRiskAppointments)).ToList(),
            queue.Select(item => new AdminNoShowRiskQueueItemDto(
                item.QueueItemId,
                item.ServiceAppointmentId,
                item.ServiceRequestId,
                item.Category,
                item.City,
                item.ProviderName,
                item.ClientName,
                item.RiskLevel.ToString(),
                item.Score,
                item.ReasonsCsv,
                item.WindowStartUtc,
                item.LastDetectedAtUtc,
                item.FirstDetectedAtUtc)).ToList());
    }

    private static (DateTime FromUtc, DateTime ToUtc) NormalizeDateRange(DateTime? fromUtc, DateTime? toUtc)
    {
        var nowUtc = DateTime.UtcNow;
        var normalizedTo = toUtc ?? nowUtc;
        var normalizedFrom = fromUtc ?? normalizedTo.AddDays(-30);

        if (normalizedFrom > normalizedTo)
        {
            (normalizedFrom, normalizedTo) = (normalizedTo, normalizedFrom);
        }

        return (normalizedFrom, normalizedTo);
    }

    private static ServiceAppointmentNoShowRiskLevel? ParseRiskLevel(string? riskLevel)
    {
        if (string.IsNullOrWhiteSpace(riskLevel))
        {
            return null;
        }

        return Enum.TryParse<ServiceAppointmentNoShowRiskLevel>(
            riskLevel.Trim(),
            ignoreCase: true,
            out var parsed)
            ? parsed
            : null;
    }

    private static decimal CalculateRate(int numerator, int denominator)
    {
        if (denominator <= 0)
        {
            return 0m;
        }

        var value = (decimal)numerator / denominator * 100m;
        return Math.Round(value, 1, MidpointRounding.AwayFromZero);
    }
}
