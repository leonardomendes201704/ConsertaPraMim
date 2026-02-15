using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using System.Globalization;
using System.Text;

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

    public async Task<string> ExportDashboardCsvAsync(AdminNoShowDashboardQueryDto query)
    {
        var dashboard = await GetDashboardAsync(query);
        return BuildCsv(dashboard);
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

    private static string BuildCsv(AdminNoShowDashboardDto dashboard)
    {
        var sb = new StringBuilder();
        AppendCsvRow(
            sb,
            "Section",
            "Name",
            "FromUtc",
            "ToUtc",
            "CityFilter",
            "CategoryFilter",
            "RiskLevelFilter",
            "BaseAppointments",
            "NoShowAppointments",
            "NoShowRatePercent",
            "AttendanceAppointments",
            "AttendanceRatePercent",
            "DualPresenceConfirmedAppointments",
            "DualPresenceConfirmationRatePercent",
            "HighRiskAppointments",
            "HighRiskConvertedAppointments",
            "HighRiskConversionRatePercent",
            "OpenQueueItems",
            "HighRiskOpenQueueItems",
            "AverageQueueAgeMinutes",
            "QueueItemId",
            "ServiceAppointmentId",
            "ServiceRequestId",
            "Category",
            "City",
            "ProviderName",
            "ClientName",
            "RiskLevel",
            "Score",
            "Reasons",
            "WindowStartUtc",
            "LastDetectedAtUtc",
            "FirstDetectedAtUtc");

        AppendCsvRow(
            sb,
            "Kpi",
            "Resumo",
            ToIso8601(dashboard.FromUtc),
            ToIso8601(dashboard.ToUtc),
            dashboard.CityFilter,
            dashboard.CategoryFilter,
            dashboard.RiskLevelFilter,
            ToInvariant(dashboard.BaseAppointments),
            ToInvariant(dashboard.NoShowAppointments),
            ToInvariant(dashboard.NoShowRatePercent),
            ToInvariant(dashboard.AttendanceAppointments),
            ToInvariant(dashboard.AttendanceRatePercent),
            ToInvariant(dashboard.DualPresenceConfirmedAppointments),
            ToInvariant(dashboard.DualPresenceConfirmationRatePercent),
            ToInvariant(dashboard.HighRiskAppointments),
            ToInvariant(dashboard.HighRiskConvertedAppointments),
            ToInvariant(dashboard.HighRiskConversionRatePercent),
            ToInvariant(dashboard.OpenQueueItems),
            ToInvariant(dashboard.HighRiskOpenQueueItems),
            ToInvariant(dashboard.AverageQueueAgeMinutes));

        foreach (var item in dashboard.NoShowByCategory)
        {
            AppendCsvRow(
                sb,
                "BreakdownCategory",
                item.Name,
                ToIso8601(dashboard.FromUtc),
                ToIso8601(dashboard.ToUtc),
                dashboard.CityFilter,
                dashboard.CategoryFilter,
                dashboard.RiskLevelFilter,
                ToInvariant(item.BaseAppointments),
                ToInvariant(item.NoShowAppointments),
                ToInvariant(item.NoShowRatePercent),
                null,
                null,
                null,
                null,
                ToInvariant(item.HighRiskAppointments));
        }

        foreach (var item in dashboard.NoShowByCity)
        {
            AppendCsvRow(
                sb,
                "BreakdownCity",
                item.Name,
                ToIso8601(dashboard.FromUtc),
                ToIso8601(dashboard.ToUtc),
                dashboard.CityFilter,
                dashboard.CategoryFilter,
                dashboard.RiskLevelFilter,
                ToInvariant(item.BaseAppointments),
                ToInvariant(item.NoShowAppointments),
                ToInvariant(item.NoShowRatePercent),
                null,
                null,
                null,
                null,
                ToInvariant(item.HighRiskAppointments));
        }

        foreach (var item in dashboard.OpenRiskQueue)
        {
            AppendCsvRow(
                sb,
                "OpenRiskQueue",
                $"{item.ProviderName} / {item.ClientName}",
                ToIso8601(dashboard.FromUtc),
                ToIso8601(dashboard.ToUtc),
                dashboard.CityFilter,
                dashboard.CategoryFilter,
                dashboard.RiskLevelFilter,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                item.QueueItemId.ToString(),
                item.ServiceAppointmentId.ToString(),
                item.ServiceRequestId.ToString(),
                item.Category,
                item.City,
                item.ProviderName,
                item.ClientName,
                item.RiskLevel,
                ToInvariant(item.Score),
                item.Reasons,
                ToIso8601(item.WindowStartUtc),
                ToIso8601(item.LastDetectedAtUtc),
                ToIso8601(item.FirstDetectedAtUtc));
        }

        return sb.ToString();
    }

    private static void AppendCsvRow(StringBuilder sb, params string?[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append(EscapeCsv(values[i]));
        }

        sb.AppendLine();
    }

    private static string ToIso8601(DateTime value)
    {
        return value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
    }

    private static string? ToInvariant<TValue>(TValue value)
    {
        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
