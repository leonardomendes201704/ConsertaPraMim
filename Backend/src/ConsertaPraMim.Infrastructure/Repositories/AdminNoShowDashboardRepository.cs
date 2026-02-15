using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class AdminNoShowDashboardRepository : IAdminNoShowDashboardRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public AdminNoShowDashboardRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task<AdminNoShowDashboardKpiReadModel> GetKpisAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? cityFilter,
        string? categoryFilter,
        ServiceAppointmentNoShowRiskLevel? riskLevelFilter,
        int cancellationNoShowWindowHours = 24)
    {
        var baseQuery = BuildAppointmentAnalyticsQuery(
            fromUtc,
            toUtc,
            cityFilter,
            categoryFilter,
            riskLevelFilter,
            cancellationNoShowWindowHours);

        var baseAppointments = await baseQuery.CountAsync();
        var noShowAppointments = await baseQuery.CountAsync(a => a.IsNoShow);
        var attendanceAppointments = await baseQuery.CountAsync(a => a.IsAttendance);
        var dualPresenceConfirmed = await baseQuery.CountAsync(a => a.IsDualPresenceConfirmed);
        var highRiskAppointments = await baseQuery.CountAsync(a => a.IsHighRisk);
        var highRiskConverted = await baseQuery.CountAsync(a => a.IsHighRisk && a.IsAttendance);

        var nowUtc = DateTime.UtcNow;
        var openQueueQuery = BuildOpenQueueQuery(fromUtc, toUtc, cityFilter, categoryFilter, riskLevelFilter);
        var openQueueItems = await openQueueQuery.CountAsync();
        var highRiskOpenQueueItems = await openQueueQuery.CountAsync(q => q.RiskLevel == ServiceAppointmentNoShowRiskLevel.High);
        var averageQueueAgeMinutes = await openQueueQuery
            .Select(q => (double?)EF.Functions.DateDiffMinute(q.FirstDetectedAtUtc, nowUtc))
            .AverageAsync() ?? 0d;

        return new AdminNoShowDashboardKpiReadModel(
            baseAppointments,
            noShowAppointments,
            attendanceAppointments,
            dualPresenceConfirmed,
            highRiskAppointments,
            highRiskConverted,
            openQueueItems,
            highRiskOpenQueueItems,
            averageQueueAgeMinutes);
    }

    public async Task<IReadOnlyList<AdminNoShowBreakdownReadModel>> GetBreakdownByCategoryAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? cityFilter,
        ServiceAppointmentNoShowRiskLevel? riskLevelFilter,
        int cancellationNoShowWindowHours = 24)
    {
        var query = BuildAppointmentAnalyticsQuery(
            fromUtc,
            toUtc,
            cityFilter,
            categoryFilter: null,
            riskLevelFilter,
            cancellationNoShowWindowHours);

        return await query
            .GroupBy(a => string.IsNullOrWhiteSpace(a.Category) ? "Sem categoria" : a.Category)
            .Select(g => new AdminNoShowBreakdownReadModel(
                g.Key,
                g.Count(),
                g.Count(x => x.IsNoShow),
                g.Count(x => x.IsHighRisk)))
            .OrderByDescending(x => x.NoShowAppointments)
            .ThenByDescending(x => x.BaseAppointments)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<AdminNoShowBreakdownReadModel>> GetBreakdownByCityAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? categoryFilter,
        ServiceAppointmentNoShowRiskLevel? riskLevelFilter,
        int cancellationNoShowWindowHours = 24)
    {
        var query = BuildAppointmentAnalyticsQuery(
            fromUtc,
            toUtc,
            cityFilter: null,
            categoryFilter,
            riskLevelFilter,
            cancellationNoShowWindowHours);

        return await query
            .GroupBy(a => string.IsNullOrWhiteSpace(a.City) ? "Sem cidade" : a.City)
            .Select(g => new AdminNoShowBreakdownReadModel(
                g.Key,
                g.Count(),
                g.Count(x => x.IsNoShow),
                g.Count(x => x.IsHighRisk)))
            .OrderByDescending(x => x.NoShowAppointments)
            .ThenByDescending(x => x.BaseAppointments)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<AdminNoShowRiskQueueItemReadModel>> GetOpenRiskQueueAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? cityFilter,
        string? categoryFilter,
        ServiceAppointmentNoShowRiskLevel? riskLevelFilter,
        int take = 100)
    {
        var normalizedTake = Math.Clamp(take, 1, 500);
        var queueQuery = BuildOpenQueueQuery(fromUtc, toUtc, cityFilter, categoryFilter, riskLevelFilter);

        return await queueQuery
            .OrderByDescending(q => q.RiskLevel)
            .ThenByDescending(q => q.Score)
            .ThenBy(q => q.ServiceAppointment.WindowStartUtc)
            .Take(normalizedTake)
            .Select(q => new AdminNoShowRiskQueueItemReadModel(
                q.Id,
                q.ServiceAppointmentId,
                q.ServiceAppointment.ServiceRequestId,
                q.ServiceAppointment.ServiceRequest.CategoryDefinitionId.HasValue
                    ? q.ServiceAppointment.ServiceRequest.CategoryDefinition!.Name
                    : q.ServiceAppointment.ServiceRequest.Category.ToString(),
                q.ServiceAppointment.ServiceRequest.AddressCity,
                q.ServiceAppointment.Provider.Name,
                q.ServiceAppointment.Client.Name,
                q.RiskLevel,
                q.Score,
                q.ReasonsCsv,
                q.ServiceAppointment.WindowStartUtc,
                q.LastDetectedAtUtc,
                q.FirstDetectedAtUtc))
            .ToListAsync();
    }

    private IQueryable<ServiceAppointmentNoShowQueueItem> BuildOpenQueueQuery(
        DateTime fromUtc,
        DateTime toUtc,
        string? cityFilter,
        string? categoryFilter,
        ServiceAppointmentNoShowRiskLevel? riskLevelFilter)
    {
        var query = _context.ServiceAppointmentNoShowQueueItems
            .AsNoTracking()
            .Where(q =>
                (q.Status == ServiceAppointmentNoShowQueueStatus.Open ||
                 q.Status == ServiceAppointmentNoShowQueueStatus.InProgress) &&
                q.ServiceAppointment.WindowStartUtc >= fromUtc &&
                q.ServiceAppointment.WindowStartUtc <= toUtc);

        if (riskLevelFilter.HasValue)
        {
            var riskLevel = riskLevelFilter.Value;
            query = query.Where(q => q.RiskLevel == riskLevel);
        }

        if (!string.IsNullOrWhiteSpace(cityFilter))
        {
            var normalizedCity = cityFilter.Trim().ToLower();
            query = query.Where(q => q.ServiceAppointment.ServiceRequest.AddressCity.ToLower().Contains(normalizedCity));
        }

        if (!string.IsNullOrWhiteSpace(categoryFilter))
        {
            var normalizedCategory = categoryFilter.Trim().ToLower();
            query = query.Where(q =>
                (q.ServiceAppointment.ServiceRequest.CategoryDefinitionId.HasValue &&
                 q.ServiceAppointment.ServiceRequest.CategoryDefinition!.Name.ToLower().Contains(normalizedCategory)) ||
                q.ServiceAppointment.ServiceRequest.Category.ToString().ToLower().Contains(normalizedCategory));
        }

        return query;
    }

    private IQueryable<AppointmentAnalyticsProjection> BuildAppointmentAnalyticsQuery(
        DateTime fromUtc,
        DateTime toUtc,
        string? cityFilter,
        string? categoryFilter,
        ServiceAppointmentNoShowRiskLevel? riskLevelFilter,
        int cancellationNoShowWindowHours)
    {
        var normalizedNoShowWindow = Math.Clamp(cancellationNoShowWindowHours, 1, 168);

        var query = _context.ServiceAppointments
            .AsNoTracking()
            .Where(a => a.WindowStartUtc >= fromUtc && a.WindowStartUtc <= toUtc)
            .Select(a => new AppointmentAnalyticsProjection
            {
                City = a.ServiceRequest.AddressCity,
                Category = a.ServiceRequest.CategoryDefinitionId.HasValue
                    ? a.ServiceRequest.CategoryDefinition!.Name
                    : a.ServiceRequest.Category.ToString(),
                RiskLevel = a.NoShowRiskLevel,
                IsNoShow =
                    a.Status == ServiceAppointmentStatus.ExpiredWithoutProviderAction ||
                    ((a.Status == ServiceAppointmentStatus.CancelledByClient ||
                      a.Status == ServiceAppointmentStatus.CancelledByProvider) &&
                     (!a.CancelledAtUtc.HasValue ||
                      EF.Functions.DateDiffHour(a.CancelledAtUtc.Value, a.WindowStartUtc) <= normalizedNoShowWindow)),
                IsAttendance =
                    a.Status == ServiceAppointmentStatus.Arrived ||
                    a.Status == ServiceAppointmentStatus.InProgress ||
                    a.Status == ServiceAppointmentStatus.Completed,
                IsDualPresenceConfirmed = a.ClientPresenceConfirmed == true && a.ProviderPresenceConfirmed == true,
                IsHighRisk = a.NoShowRiskLevel == ServiceAppointmentNoShowRiskLevel.High
            });

        if (riskLevelFilter.HasValue)
        {
            var riskLevel = riskLevelFilter.Value;
            query = query.Where(a => a.RiskLevel == riskLevel);
        }

        if (!string.IsNullOrWhiteSpace(cityFilter))
        {
            var normalizedCity = cityFilter.Trim().ToLower();
            query = query.Where(a => a.City.ToLower().Contains(normalizedCity));
        }

        if (!string.IsNullOrWhiteSpace(categoryFilter))
        {
            var normalizedCategory = categoryFilter.Trim().ToLower();
            query = query.Where(a => a.Category.ToLower().Contains(normalizedCategory));
        }

        return query;
    }

    private sealed class AppointmentAnalyticsProjection
    {
        public string City { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public ServiceAppointmentNoShowRiskLevel? RiskLevel { get; set; }
        public bool IsNoShow { get; set; }
        public bool IsAttendance { get; set; }
        public bool IsDualPresenceConfirmed { get; set; }
        public bool IsHighRisk { get; set; }
    }
}
