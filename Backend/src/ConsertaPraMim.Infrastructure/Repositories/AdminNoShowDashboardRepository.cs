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
        if (!_context.Database.IsSqlServer())
        {
            return await GetKpisUsingInMemoryAggregationAsync(
                fromUtc,
                toUtc,
                cityFilter,
                categoryFilter,
                riskLevelFilter,
                cancellationNoShowWindowHours);
        }

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
        var firstDetectedList = await openQueueQuery
            .Select(q => q.FirstDetectedAtUtc)
            .ToListAsync();
        var averageQueueAgeMinutes = firstDetectedList.Count == 0
            ? 0d
            : firstDetectedList.Average(firstDetectedAtUtc =>
                Math.Max(0d, Math.Floor((nowUtc - firstDetectedAtUtc).TotalMinutes)));

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
        if (!_context.Database.IsSqlServer())
        {
            return await GetBreakdownByCategoryUsingInMemoryAggregationAsync(
                fromUtc,
                toUtc,
                cityFilter,
                riskLevelFilter,
                cancellationNoShowWindowHours);
        }

        var query = BuildAppointmentAnalyticsQuery(
            fromUtc,
            toUtc,
            cityFilter,
            categoryFilter: null,
            riskLevelFilter,
            cancellationNoShowWindowHours);

        var grouped = await query
            .GroupBy(a => new
            {
                a.CategoryDefinitionName,
                a.CategoryLegacy
            })
            .Select(g => new CategoryBreakdownProjection
            {
                CategoryDefinitionName = g.Key.CategoryDefinitionName,
                CategoryLegacy = g.Key.CategoryLegacy,
                BaseAppointments = g.Count(),
                NoShowAppointments = g.Count(x => x.IsNoShow),
                HighRiskAppointments = g.Count(x => x.IsHighRisk)
            })
            .ToListAsync();

        return grouped
            .Select(item => new AdminNoShowBreakdownReadModel(
                ResolveCategoryName(item.CategoryDefinitionName, item.CategoryLegacy),
                item.BaseAppointments,
                item.NoShowAppointments,
                item.HighRiskAppointments))
            .OrderByDescending(x => x.NoShowAppointments)
            .ThenByDescending(x => x.BaseAppointments)
            .ThenBy(x => x.Name)
            .ToList();
    }

    public async Task<IReadOnlyList<AdminNoShowBreakdownReadModel>> GetBreakdownByCityAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? categoryFilter,
        ServiceAppointmentNoShowRiskLevel? riskLevelFilter,
        int cancellationNoShowWindowHours = 24)
    {
        if (!_context.Database.IsSqlServer())
        {
            return await GetBreakdownByCityUsingInMemoryAggregationAsync(
                fromUtc,
                toUtc,
                categoryFilter,
                riskLevelFilter,
                cancellationNoShowWindowHours);
        }

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

        var items = await queueQuery
            .OrderByDescending(q => q.RiskLevel)
            .ThenByDescending(q => q.Score)
            .ThenBy(q => q.ServiceAppointment.WindowStartUtc)
            .Take(normalizedTake)
            .Select(q => new OpenRiskQueueProjection
            {
                QueueItemId = q.Id,
                ServiceAppointmentId = q.ServiceAppointmentId,
                ServiceRequestId = q.ServiceAppointment.ServiceRequestId,
                CategoryDefinitionName = q.ServiceAppointment.ServiceRequest.CategoryDefinitionId.HasValue
                    ? q.ServiceAppointment.ServiceRequest.CategoryDefinition!.Name
                    : null,
                CategoryLegacy = q.ServiceAppointment.ServiceRequest.Category,
                City = q.ServiceAppointment.ServiceRequest.AddressCity,
                ProviderName = q.ServiceAppointment.Provider.Name,
                ClientName = q.ServiceAppointment.Client.Name,
                RiskLevel = q.RiskLevel,
                Score = q.Score,
                Reasons = q.ReasonsCsv,
                WindowStartUtc = q.ServiceAppointment.WindowStartUtc,
                LastDetectedAtUtc = q.LastDetectedAtUtc,
                FirstDetectedAtUtc = q.FirstDetectedAtUtc
            })
            .ToListAsync();

        return items
            .Select(item => new AdminNoShowRiskQueueItemReadModel(
                item.QueueItemId,
                item.ServiceAppointmentId,
                item.ServiceRequestId,
                ResolveCategoryName(item.CategoryDefinitionName, item.CategoryLegacy),
                item.City,
                item.ProviderName,
                item.ClientName,
                item.RiskLevel,
                item.Score,
                item.Reasons,
                item.WindowStartUtc,
                item.LastDetectedAtUtc,
                item.FirstDetectedAtUtc))
            .ToList();
    }

    private async Task<AdminNoShowDashboardKpiReadModel> GetKpisUsingInMemoryAggregationAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? cityFilter,
        string? categoryFilter,
        ServiceAppointmentNoShowRiskLevel? riskLevelFilter,
        int cancellationNoShowWindowHours)
    {
        var analytics = await LoadAppointmentAnalyticsForInMemoryAggregationAsync(
            fromUtc,
            toUtc,
            cityFilter,
            categoryFilter,
            riskLevelFilter,
            cancellationNoShowWindowHours);

        var baseAppointments = analytics.Count;
        var noShowAppointments = analytics.Count(a => a.IsNoShow);
        var attendanceAppointments = analytics.Count(a => a.IsAttendance);
        var dualPresenceConfirmed = analytics.Count(a => a.IsDualPresenceConfirmed);
        var highRiskAppointments = analytics.Count(a => a.IsHighRisk);
        var highRiskConverted = analytics.Count(a => a.IsHighRisk && a.IsAttendance);

        var nowUtc = DateTime.UtcNow;
        var openQueueQuery = BuildOpenQueueQuery(fromUtc, toUtc, cityFilter, categoryFilter, riskLevelFilter);
        var openQueueItems = await openQueueQuery.CountAsync();
        var highRiskOpenQueueItems = await openQueueQuery.CountAsync(q => q.RiskLevel == ServiceAppointmentNoShowRiskLevel.High);
        var firstDetectedList = await openQueueQuery
            .Select(q => q.FirstDetectedAtUtc)
            .ToListAsync();
        var averageQueueAgeMinutes = firstDetectedList.Count == 0
            ? 0d
            : firstDetectedList.Average(firstDetectedAtUtc =>
                Math.Max(0d, Math.Floor((nowUtc - firstDetectedAtUtc).TotalMinutes)));

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

    private async Task<IReadOnlyList<AdminNoShowBreakdownReadModel>> GetBreakdownByCategoryUsingInMemoryAggregationAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? cityFilter,
        ServiceAppointmentNoShowRiskLevel? riskLevelFilter,
        int cancellationNoShowWindowHours)
    {
        var analytics = await LoadAppointmentAnalyticsForInMemoryAggregationAsync(
            fromUtc,
            toUtc,
            cityFilter,
            categoryFilter: null,
            riskLevelFilter,
            cancellationNoShowWindowHours);

        return analytics
            .GroupBy(a => ResolveCategoryName(a.CategoryDefinitionName, a.CategoryLegacy))
            .Select(g => new AdminNoShowBreakdownReadModel(
                g.Key,
                g.Count(),
                g.Count(x => x.IsNoShow),
                g.Count(x => x.IsHighRisk)))
            .OrderByDescending(x => x.NoShowAppointments)
            .ThenByDescending(x => x.BaseAppointments)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private async Task<IReadOnlyList<AdminNoShowBreakdownReadModel>> GetBreakdownByCityUsingInMemoryAggregationAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? categoryFilter,
        ServiceAppointmentNoShowRiskLevel? riskLevelFilter,
        int cancellationNoShowWindowHours)
    {
        var analytics = await LoadAppointmentAnalyticsForInMemoryAggregationAsync(
            fromUtc,
            toUtc,
            cityFilter: null,
            categoryFilter,
            riskLevelFilter,
            cancellationNoShowWindowHours);

        return analytics
            .GroupBy(a => string.IsNullOrWhiteSpace(a.City) ? "Sem cidade" : a.City)
            .Select(g => new AdminNoShowBreakdownReadModel(
                g.Key,
                g.Count(),
                g.Count(x => x.IsNoShow),
                g.Count(x => x.IsHighRisk)))
            .OrderByDescending(x => x.NoShowAppointments)
            .ThenByDescending(x => x.BaseAppointments)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private async Task<List<AppointmentAnalyticsProjection>> LoadAppointmentAnalyticsForInMemoryAggregationAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? cityFilter,
        string? categoryFilter,
        ServiceAppointmentNoShowRiskLevel? riskLevelFilter,
        int cancellationNoShowWindowHours)
    {
        var normalizedNoShowWindow = Math.Clamp(cancellationNoShowWindowHours, 1, 168);
        var sourceRows = await BuildAppointmentAnalyticsRawQuery(
                fromUtc,
                toUtc,
                cityFilter,
                categoryFilter,
                riskLevelFilter)
            .ToListAsync();

        return sourceRows
            .Select(row => new AppointmentAnalyticsProjection
            {
                City = row.City,
                CategoryDefinitionName = row.CategoryDefinitionName,
                CategoryLegacy = row.CategoryLegacy,
                RiskLevel = row.RiskLevel,
                IsNoShow = IsNoShow(row.Status, row.CancelledAtUtc, row.WindowStartUtc, normalizedNoShowWindow),
                IsAttendance = row.Status is ServiceAppointmentStatus.Arrived
                    or ServiceAppointmentStatus.InProgress
                    or ServiceAppointmentStatus.Completed,
                IsDualPresenceConfirmed = row.ClientPresenceConfirmed == true && row.ProviderPresenceConfirmed == true,
                IsHighRisk = row.RiskLevel == ServiceAppointmentNoShowRiskLevel.High
            })
            .ToList();
    }

    private IQueryable<AppointmentAnalyticsRawProjection> BuildAppointmentAnalyticsRawQuery(
        DateTime fromUtc,
        DateTime toUtc,
        string? cityFilter,
        string? categoryFilter,
        ServiceAppointmentNoShowRiskLevel? riskLevelFilter)
    {
        var query = _context.ServiceAppointments
            .AsNoTracking()
            .Where(a => a.WindowStartUtc >= fromUtc && a.WindowStartUtc <= toUtc)
            .Select(a => new AppointmentAnalyticsRawProjection
            {
                City = a.ServiceRequest.AddressCity,
                CategoryDefinitionName = a.ServiceRequest.CategoryDefinitionId.HasValue
                    ? a.ServiceRequest.CategoryDefinition!.Name
                    : null,
                CategoryLegacy = a.ServiceRequest.Category,
                RiskLevel = a.NoShowRiskLevel,
                Status = a.Status,
                CancelledAtUtc = a.CancelledAtUtc,
                WindowStartUtc = a.WindowStartUtc,
                ClientPresenceConfirmed = a.ClientPresenceConfirmed,
                ProviderPresenceConfirmed = a.ProviderPresenceConfirmed
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
            var parsedCategory = ParseLegacyCategoryFilter(normalizedCategory);
            query = query.Where(a =>
                (a.CategoryDefinitionName != null && a.CategoryDefinitionName.ToLower().Contains(normalizedCategory)) ||
                (parsedCategory.HasValue && a.CategoryLegacy == parsedCategory.Value));
        }

        return query;
    }

    private static bool IsNoShow(
        ServiceAppointmentStatus status,
        DateTime? cancelledAtUtc,
        DateTime windowStartUtc,
        int normalizedNoShowWindow)
    {
        if (status == ServiceAppointmentStatus.ExpiredWithoutProviderAction)
        {
            return true;
        }

        if (status is not ServiceAppointmentStatus.CancelledByClient and not ServiceAppointmentStatus.CancelledByProvider)
        {
            return false;
        }

        if (!cancelledAtUtc.HasValue)
        {
            return true;
        }

        return cancelledAtUtc.Value >= windowStartUtc.AddHours(-normalizedNoShowWindow);
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
            var parsedCategory = ParseLegacyCategoryFilter(normalizedCategory);
            query = query.Where(q =>
                (q.ServiceAppointment.ServiceRequest.CategoryDefinitionId.HasValue &&
                 q.ServiceAppointment.ServiceRequest.CategoryDefinition!.Name.ToLower().Contains(normalizedCategory)) ||
                (parsedCategory.HasValue &&
                 q.ServiceAppointment.ServiceRequest.Category == parsedCategory.Value));
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
                CategoryDefinitionName = a.ServiceRequest.CategoryDefinitionId.HasValue
                    ? a.ServiceRequest.CategoryDefinition!.Name
                    : null,
                CategoryLegacy = a.ServiceRequest.Category,
                RiskLevel = a.NoShowRiskLevel,
                IsNoShow =
                    a.Status == ServiceAppointmentStatus.ExpiredWithoutProviderAction ||
                    ((a.Status == ServiceAppointmentStatus.CancelledByClient ||
                      a.Status == ServiceAppointmentStatus.CancelledByProvider) &&
                     (!a.CancelledAtUtc.HasValue ||
                      a.CancelledAtUtc.Value >= a.WindowStartUtc.AddHours(-normalizedNoShowWindow))),
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
            var parsedCategory = ParseLegacyCategoryFilter(normalizedCategory);
            query = query.Where(a =>
                (a.CategoryDefinitionName != null && a.CategoryDefinitionName.ToLower().Contains(normalizedCategory)) ||
                (parsedCategory.HasValue && a.CategoryLegacy == parsedCategory.Value));
        }

        return query;
    }

    private static string ResolveCategoryName(string? categoryDefinitionName, ServiceCategory categoryLegacy)
    {
        if (!string.IsNullOrWhiteSpace(categoryDefinitionName))
        {
            return categoryDefinitionName;
        }

        return categoryLegacy.ToString();
    }

    private static ServiceCategory? ParseLegacyCategoryFilter(string rawCategoryFilter)
    {
        if (string.IsNullOrWhiteSpace(rawCategoryFilter))
        {
            return null;
        }

        var normalized = NormalizeCategoryToken(rawCategoryFilter);

        if (Enum.TryParse<ServiceCategory>(normalized, ignoreCase: true, out var parsedEnum))
        {
            return parsedEnum;
        }

        return normalized switch
        {
            "eletrica" => ServiceCategory.Electrical,
            "hidraulica" => ServiceCategory.Plumbing,
            "eletronicos" => ServiceCategory.Electronics,
            "eletrodomesticos" => ServiceCategory.Appliances,
            "alvenaria" => ServiceCategory.Masonry,
            "limpeza" => ServiceCategory.Cleaning,
            "outros" => ServiceCategory.Other,
            _ => null
        };
    }

    private static string NormalizeCategoryToken(string value)
    {
        return value
            .Trim()
            .ToLowerInvariant()
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty);
    }

    private sealed class CategoryBreakdownProjection
    {
        public string? CategoryDefinitionName { get; set; }
        public ServiceCategory CategoryLegacy { get; set; }
        public int BaseAppointments { get; set; }
        public int NoShowAppointments { get; set; }
        public int HighRiskAppointments { get; set; }
    }

    private sealed class OpenRiskQueueProjection
    {
        public Guid QueueItemId { get; set; }
        public Guid ServiceAppointmentId { get; set; }
        public Guid ServiceRequestId { get; set; }
        public string? CategoryDefinitionName { get; set; }
        public ServiceCategory CategoryLegacy { get; set; }
        public string City { get; set; } = string.Empty;
        public string ProviderName { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public ServiceAppointmentNoShowRiskLevel RiskLevel { get; set; }
        public int Score { get; set; }
        public string? Reasons { get; set; }
        public DateTime WindowStartUtc { get; set; }
        public DateTime LastDetectedAtUtc { get; set; }
        public DateTime FirstDetectedAtUtc { get; set; }
    }

    private sealed class AppointmentAnalyticsRawProjection
    {
        public string City { get; set; } = string.Empty;
        public string? CategoryDefinitionName { get; set; }
        public ServiceCategory CategoryLegacy { get; set; }
        public ServiceAppointmentNoShowRiskLevel? RiskLevel { get; set; }
        public ServiceAppointmentStatus Status { get; set; }
        public DateTime? CancelledAtUtc { get; set; }
        public DateTime WindowStartUtc { get; set; }
        public bool? ClientPresenceConfirmed { get; set; }
        public bool? ProviderPresenceConfirmed { get; set; }
    }

    private sealed class AppointmentAnalyticsProjection
    {
        public string City { get; set; } = string.Empty;
        public string? CategoryDefinitionName { get; set; }
        public ServiceCategory CategoryLegacy { get; set; }
        public ServiceAppointmentNoShowRiskLevel? RiskLevel { get; set; }
        public bool IsNoShow { get; set; }
        public bool IsAttendance { get; set; }
        public bool IsDualPresenceConfirmed { get; set; }
        public bool IsHighRisk { get; set; }
    }
}
