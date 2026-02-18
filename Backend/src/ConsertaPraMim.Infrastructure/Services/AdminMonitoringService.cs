using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConsertaPraMim.Infrastructure.Services;

public class AdminMonitoringService : IAdminMonitoringService
{
    private readonly ConsertaPraMimDbContext _dbContext;
    private readonly ILogger<AdminMonitoringService> _logger;

    public AdminMonitoringService(
        ConsertaPraMimDbContext dbContext,
        ILogger<AdminMonitoringService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<int> SaveRawEventsAsync(
        IReadOnlyCollection<ApiRequestTelemetryEventDto> events,
        CancellationToken cancellationToken = default)
    {
        if (events.Count == 0)
        {
            return 0;
        }

        var entities = events
            .Select(MapToEntity)
            .ToList();

        await _dbContext.ApiRequestLogs.AddRangeAsync(entities, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entities.Count;
    }

    public async Task<AdminMonitoringMaintenanceResultDto> RebuildAggregatesAndRetentionAsync(
        AdminMonitoringMaintenanceOptionsDto options,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var hourlyWindowHours = Math.Clamp(options.HourlyRecomputeWindowHours, 1, 168);
        var dailyWindowDays = Math.Clamp(options.DailyRecomputeWindowDays, 1, 365);
        var rawRetentionDays = Math.Clamp(options.RawRetentionDays, 1, 180);
        var aggregateRetentionDays = Math.Clamp(options.AggregateRetentionDays, 7, 730);

        var hourlyFromUtc = TruncateToHour(nowUtc.AddHours(-hourlyWindowHours));
        var dailyFromUtc = TruncateToDay(nowUtc.AddDays(-(dailyWindowDays - 1)));

        var hourlyLogs = await _dbContext.ApiRequestLogs
            .AsNoTracking()
            .Where(x => x.TimestampUtc >= hourlyFromUtc && x.TimestampUtc <= nowUtc)
            .ToListAsync(cancellationToken);

        var dailyLogs = dailyFromUtc <= hourlyFromUtc
            ? hourlyLogs.Where(x => x.TimestampUtc >= dailyFromUtc).ToList()
            : await _dbContext.ApiRequestLogs
                .AsNoTracking()
                .Where(x => x.TimestampUtc >= dailyFromUtc && x.TimestampUtc <= nowUtc)
                .ToListAsync(cancellationToken);

        var recomputedHourlyBuckets = hourlyLogs
            .Select(x => TruncateToHour(x.TimestampUtc))
            .Distinct()
            .Count();

        var recomputedDailyBuckets = dailyLogs
            .Select(x => TruncateToDay(x.TimestampUtc))
            .Distinct()
            .Count();

        var hourlyRows = BuildHourlyMetrics(hourlyLogs);
        var dailyRows = BuildDailyMetrics(dailyLogs);

        await _dbContext.ApiEndpointMetricsHourly
            .Where(x => x.BucketStartUtc >= hourlyFromUtc)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.ApiEndpointMetricsDaily
            .Where(x => x.BucketDateUtc >= dailyFromUtc)
            .ExecuteDeleteAsync(cancellationToken);

        if (hourlyRows.Count > 0)
        {
            await _dbContext.ApiEndpointMetricsHourly.AddRangeAsync(hourlyRows, cancellationToken);
        }

        if (dailyRows.Count > 0)
        {
            await _dbContext.ApiEndpointMetricsDaily.AddRangeAsync(dailyRows, cancellationToken);
        }

        var errorLogs = hourlyLogs
            .Where(x => !string.IsNullOrWhiteSpace(x.NormalizedErrorKey))
            .ToList();

        var updatedCatalogCount = await UpsertErrorCatalogAsync(errorLogs, cancellationToken);
        var upsertedErrorOccurrences = await UpsertHourlyErrorOccurrencesAsync(errorLogs, hourlyFromUtc, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var rawCutoffUtc = nowUtc.AddDays(-rawRetentionDays);
        var aggregateCutoffUtc = nowUtc.AddDays(-aggregateRetentionDays);
        var aggregateDayCutoffUtc = TruncateToDay(aggregateCutoffUtc);

        var purgedRawLogs = await _dbContext.ApiRequestLogs
            .Where(x => x.TimestampUtc < rawCutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);

        var purgedHourlyAggregates = await _dbContext.ApiEndpointMetricsHourly
            .Where(x => x.BucketStartUtc < aggregateCutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);

        var purgedDailyAggregates = await _dbContext.ApiEndpointMetricsDaily
            .Where(x => x.BucketDateUtc < aggregateDayCutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);

        var purgedErrorOccurrences = await _dbContext.ApiErrorOccurrencesHourly
            .Where(x => x.BucketStartUtc < aggregateCutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);

        return new AdminMonitoringMaintenanceResultDto(
            ProcessedRawLogs: hourlyLogs.Count,
            RecomputedHourlyBuckets: recomputedHourlyBuckets,
            RecomputedDailyBuckets: recomputedDailyBuckets,
            UpdatedErrorCatalogEntries: updatedCatalogCount,
            UpsertedErrorOccurrences: upsertedErrorOccurrences,
            PurgedRawLogs: purgedRawLogs,
            PurgedAggregateRows: purgedHourlyAggregates + purgedDailyAggregates,
            PurgedErrorOccurrences: purgedErrorOccurrences);
    }

    public async Task<AdminMonitoringOverviewDto> GetOverviewAsync(
        AdminMonitoringOverviewQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var range = ResolveRange(query.Range);

        var logs = await ApplyFilters(
                _dbContext.ApiRequestLogs.AsNoTracking(),
                range,
                query.Endpoint,
                query.StatusCode,
                query.UserId,
                query.TenantId,
                query.Severity)
            .Select(x => new RequestProjection(
                x.TimestampUtc,
                x.Method,
                x.EndpointTemplate,
                x.StatusCode,
                x.DurationMs,
                x.Severity,
                x.IsError,
                x.WarningCount,
                x.NormalizedErrorKey,
                x.ErrorType,
                x.NormalizedErrorMessage))
            .ToListAsync(cancellationToken);

        if (logs.Count == 0)
        {
            return new AdminMonitoringOverviewDto(
                TotalRequests: 0,
                ErrorRatePercent: 0,
                P95LatencyMs: 0,
                RequestsPerMinute: 0,
                TopEndpoint: "-",
                RequestsSeries: [],
                ErrorsSeries: [],
                LatencySeries: [],
                StatusDistribution: [],
                TopErrors: []);
        }

        var totalRequests = logs.Count;
        var errorCount = logs.Count(x => IsError(x.StatusCode, x.IsError));
        var p95Latency = CalculatePercentile(logs.Select(x => x.DurationMs), 0.95);
        var requestsPerMinute = totalRequests / Math.Max(1d, range.Duration.TotalMinutes);
        var topEndpoint = logs
            .GroupBy(x => $"{x.Method} {x.EndpointTemplate}")
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? "-";

        var bucketMinutes = ResolveBucketMinutes(range);
        var requestSeries = logs
            .GroupBy(x => TruncateToBucket(x.TimestampUtc, bucketMinutes))
            .OrderBy(g => g.Key)
            .Select(g => new AdminMonitoringTimeseriesPointDto(g.Key, g.LongCount()))
            .ToList();

        var errorSeries = logs
            .Where(x => x.IsError || x.StatusCode >= 500)
            .GroupBy(x => TruncateToBucket(x.TimestampUtc, bucketMinutes))
            .OrderBy(g => g.Key)
            .Select(g => new AdminMonitoringTimeseriesPointDto(g.Key, g.LongCount()))
            .ToList();

        var latencySeries = logs
            .GroupBy(x => TruncateToBucket(x.TimestampUtc, bucketMinutes))
            .OrderBy(g => g.Key)
            .Select(g => new AdminMonitoringLatencyTimeseriesPointDto(
                g.Key,
                CalculatePercentile(g.Select(x => x.DurationMs), 0.50),
                CalculatePercentile(g.Select(x => x.DurationMs), 0.95),
                CalculatePercentile(g.Select(x => x.DurationMs), 0.99)))
            .ToList();

        var statusDistribution = logs
            .GroupBy(x => x.StatusCode)
            .OrderBy(g => g.Key)
            .Select(g => new AdminMonitoringStatusDistributionDto(g.Key, g.LongCount()))
            .ToList();

        var topErrors = logs
            .Where(x => !string.IsNullOrWhiteSpace(x.NormalizedErrorKey))
            .GroupBy(x => x.NormalizedErrorKey!)
            .OrderByDescending(g => g.LongCount())
            .Take(10)
            .Select(g =>
            {
                var first = g.First();
                var topEndpointGroup = g
                    .GroupBy(x => x.EndpointTemplate)
                    .OrderByDescending(x => x.LongCount())
                    .First();

                var topStatusGroup = g
                    .GroupBy(x => x.StatusCode)
                    .OrderByDescending(x => x.LongCount())
                    .First();

                return new AdminMonitoringTopErrorDto(
                    ErrorKey: g.Key,
                    ErrorType: first.ErrorType ?? "UnknownError",
                    Message: first.NormalizedErrorMessage ?? "Erro sem mensagem normalizada",
                    Count: g.LongCount(),
                    EndpointTemplate: topEndpointGroup.Key,
                    StatusCode: topStatusGroup.Key);
            })
            .ToList();

        return new AdminMonitoringOverviewDto(
            TotalRequests: totalRequests,
            ErrorRatePercent: RoundPercent(errorCount, totalRequests),
            P95LatencyMs: p95Latency,
            RequestsPerMinute: Math.Round(requestsPerMinute, 2),
            TopEndpoint: topEndpoint,
            RequestsSeries: requestSeries,
            ErrorsSeries: errorSeries,
            LatencySeries: latencySeries,
            StatusDistribution: statusDistribution,
            TopErrors: topErrors);
    }

    public async Task<AdminMonitoringTopEndpointsResponseDto> GetTopEndpointsAsync(
        AdminMonitoringTopEndpointsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var range = ResolveRange(query.Range);
        var take = Math.Clamp(query.Take, 1, 100);

        var logs = await ApplyFilters(
                _dbContext.ApiRequestLogs.AsNoTracking(),
                range,
                query.Endpoint,
                query.StatusCode,
                query.UserId,
                query.TenantId,
                query.Severity)
            .Select(x => new
            {
                x.Method,
                x.EndpointTemplate,
                x.StatusCode,
                x.IsError,
                x.WarningCount,
                x.DurationMs
            })
            .ToListAsync(cancellationToken);

        var items = logs
            .GroupBy(x => new { x.Method, x.EndpointTemplate })
            .Select(g => new AdminMonitoringTopEndpointDto(
                Method: g.Key.Method,
                EndpointTemplate: g.Key.EndpointTemplate,
                Hits: g.LongCount(),
                ErrorRatePercent: RoundPercent(
                    g.LongCount(x => IsError(x.StatusCode, x.IsError)),
                    g.LongCount()),
                P95LatencyMs: CalculatePercentile(g.Select(x => x.DurationMs), 0.95),
                P99LatencyMs: CalculatePercentile(g.Select(x => x.DurationMs), 0.99),
                WarningCount: g.Sum(x => (long)x.WarningCount)))
            .OrderByDescending(x => x.Hits)
            .ThenByDescending(x => x.P95LatencyMs)
            .Take(take)
            .ToList();

        return new AdminMonitoringTopEndpointsResponseDto(items);
    }

    public async Task<AdminMonitoringLatencyResponseDto> GetLatencyAsync(
        AdminMonitoringLatencyQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var range = ResolveRange(query.Range);
        var normalizedEndpoint = string.IsNullOrWhiteSpace(query.Endpoint)
            ? null
            : query.Endpoint.Trim();

        var logs = await ApplyFilters(
                _dbContext.ApiRequestLogs.AsNoTracking(),
                range,
                normalizedEndpoint,
                query.StatusCode,
                query.UserId,
                query.TenantId,
                query.Severity)
            .Select(x => new
            {
                x.TimestampUtc,
                x.DurationMs,
                x.EndpointTemplate
            })
            .ToListAsync(cancellationToken);

        if (logs.Count == 0)
        {
            return new AdminMonitoringLatencyResponseDto(
                EndpointTemplate: normalizedEndpoint ?? "Todos endpoints",
                Series: [],
                P50Ms: 0,
                P95Ms: 0,
                P99Ms: 0,
                MinMs: 0,
                MaxMs: 0);
        }

        var endpointName = normalizedEndpoint ?? logs
            .GroupBy(x => x.EndpointTemplate)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? "Todos endpoints";

        var bucketMinutes = ResolveBucketMinutes(range);
        var series = logs
            .GroupBy(x => TruncateToBucket(x.TimestampUtc, bucketMinutes))
            .OrderBy(g => g.Key)
            .Select(g => new AdminMonitoringLatencyTimeseriesPointDto(
                g.Key,
                CalculatePercentile(g.Select(x => x.DurationMs), 0.50),
                CalculatePercentile(g.Select(x => x.DurationMs), 0.95),
                CalculatePercentile(g.Select(x => x.DurationMs), 0.99)))
            .ToList();

        return new AdminMonitoringLatencyResponseDto(
            EndpointTemplate: endpointName,
            Series: series,
            P50Ms: CalculatePercentile(logs.Select(x => x.DurationMs), 0.50),
            P95Ms: CalculatePercentile(logs.Select(x => x.DurationMs), 0.95),
            P99Ms: CalculatePercentile(logs.Select(x => x.DurationMs), 0.99),
            MinMs: logs.Min(x => x.DurationMs),
            MaxMs: logs.Max(x => x.DurationMs));
    }

    public async Task<AdminMonitoringErrorsResponseDto> GetErrorsAsync(
        AdminMonitoringErrorsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var range = ResolveRange(query.Range);
        var groupBy = NormalizeGroupBy(query.GroupBy);

        var logs = await ApplyFilters(
                _dbContext.ApiRequestLogs.AsNoTracking(),
                range,
                query.Endpoint,
                query.StatusCode,
                query.UserId,
                query.TenantId,
                query.Severity)
            .Where(x => x.IsError || x.StatusCode >= 500)
            .Select(x => new RequestProjection(
                x.TimestampUtc,
                x.Method,
                x.EndpointTemplate,
                x.StatusCode,
                x.DurationMs,
                x.Severity,
                x.IsError,
                x.WarningCount,
                x.NormalizedErrorKey,
                x.ErrorType,
                x.NormalizedErrorMessage))
            .ToListAsync(cancellationToken);

        var items = groupBy switch
        {
            "endpoint" => logs
                .GroupBy(x => x.EndpointTemplate)
                .OrderByDescending(g => g.LongCount())
                .Take(30)
                .Select(g => new AdminMonitoringTopErrorDto(
                    ErrorKey: g.Key,
                    ErrorType: "Endpoint",
                    Message: $"Erros concentrados em {g.Key}",
                    Count: g.LongCount(),
                    EndpointTemplate: g.Key,
                    StatusCode: g.GroupBy(x => x.StatusCode).OrderByDescending(x => x.LongCount()).Select(x => (int?)x.Key).FirstOrDefault()))
                .ToList(),
            "status" => logs
                .GroupBy(x => x.StatusCode)
                .OrderByDescending(g => g.LongCount())
                .Take(30)
                .Select(g => new AdminMonitoringTopErrorDto(
                    ErrorKey: g.Key.ToString(),
                    ErrorType: "StatusCode",
                    Message: $"Erros com status {g.Key}",
                    Count: g.LongCount(),
                    EndpointTemplate: g.GroupBy(x => x.EndpointTemplate).OrderByDescending(x => x.LongCount()).Select(x => x.Key).FirstOrDefault(),
                    StatusCode: g.Key))
                .ToList(),
            _ => logs
                .GroupBy(x => x.NormalizedErrorKey ?? $"{x.ErrorType ?? "Unknown"}|{x.StatusCode}")
                .OrderByDescending(g => g.LongCount())
                .Take(30)
                .Select(g =>
                {
                    var first = g.First();
                    return new AdminMonitoringTopErrorDto(
                        ErrorKey: g.Key,
                        ErrorType: first.ErrorType ?? "UnknownError",
                        Message: first.NormalizedErrorMessage ?? "Erro sem mensagem normalizada",
                        Count: g.LongCount(),
                        EndpointTemplate: g.GroupBy(x => x.EndpointTemplate).OrderByDescending(x => x.LongCount()).Select(x => x.Key).FirstOrDefault(),
                        StatusCode: g.GroupBy(x => x.StatusCode).OrderByDescending(x => x.LongCount()).Select(x => (int?)x.Key).FirstOrDefault());
                })
                .ToList()
        };

        var bucketMinutes = ResolveBucketMinutes(range);
        var series = logs
            .GroupBy(x => TruncateToBucket(x.TimestampUtc, bucketMinutes))
            .OrderBy(g => g.Key)
            .Select(g => new AdminMonitoringTimeseriesPointDto(g.Key, g.LongCount()))
            .ToList();

        return new AdminMonitoringErrorsResponseDto(groupBy, items, series);
    }

    public async Task<AdminMonitoringRequestsResponseDto> GetRequestsAsync(
        AdminMonitoringRequestsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var range = ResolveRange(query.Range);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var filtered = ApplyFilters(
            _dbContext.ApiRequestLogs.AsNoTracking(),
            range,
            query.Endpoint,
            query.StatusCode,
            query.UserId,
            query.TenantId,
            query.Severity);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            filtered = filtered.Where(x =>
                x.CorrelationId.Contains(search) ||
                x.Method.Contains(search) ||
                x.EndpointTemplate.Contains(search) ||
                (x.ErrorType != null && x.ErrorType.Contains(search)) ||
                (x.NormalizedErrorMessage != null && x.NormalizedErrorMessage.Contains(search)) ||
                x.Path.Contains(search));
        }

        var total = await filtered.CountAsync(cancellationToken);

        var items = await filtered
            .OrderByDescending(x => x.TimestampUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminMonitoringRequestItemDto(
                x.Id,
                x.TimestampUtc,
                x.CorrelationId,
                x.Method,
                x.EndpointTemplate,
                x.StatusCode,
                x.DurationMs,
                x.Severity,
                x.WarningCount,
                x.ErrorType,
                x.NormalizedErrorMessage,
                x.UserId,
                x.TenantId))
            .ToListAsync(cancellationToken);

        return new AdminMonitoringRequestsResponseDto(
            Page: page,
            PageSize: pageSize,
            Total: total,
            Items: items);
    }

    public async Task<AdminMonitoringRequestDetailsDto?> GetRequestByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return null;
        }

        var normalized = correlationId.Trim();
        var entity = await _dbContext.ApiRequestLogs
            .AsNoTracking()
            .Where(x => x.CorrelationId == normalized)
            .OrderByDescending(x => x.TimestampUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            return null;
        }

        return new AdminMonitoringRequestDetailsDto(
            entity.Id,
            entity.TimestampUtc,
            entity.CorrelationId,
            entity.TraceId,
            entity.Method,
            entity.EndpointTemplate,
            entity.Path,
            entity.StatusCode,
            entity.DurationMs,
            entity.Severity,
            entity.IsError,
            entity.WarningCount,
            entity.WarningCodesJson,
            entity.ErrorType,
            entity.NormalizedErrorMessage,
            entity.NormalizedErrorKey,
            entity.IpHash,
            entity.UserAgent,
            entity.UserId,
            entity.TenantId,
            entity.RequestSizeBytes,
            entity.ResponseSizeBytes,
            entity.Scheme,
            entity.Host);
    }

    private async Task<int> UpsertErrorCatalogAsync(
        IReadOnlyCollection<ApiRequestLog> errorLogs,
        CancellationToken cancellationToken)
    {
        if (errorLogs.Count == 0)
        {
            return 0;
        }

        var grouped = errorLogs
            .Where(x => !string.IsNullOrWhiteSpace(x.NormalizedErrorKey))
            .GroupBy(x => x.NormalizedErrorKey!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                ErrorKey = g.Key,
                ErrorType = g.Select(x => x.ErrorType).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "UnknownError",
                Message = g.Select(x => x.NormalizedErrorMessage).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "Erro sem mensagem normalizada",
                FirstSeenUtc = g.Min(x => x.TimestampUtc),
                LastSeenUtc = g.Max(x => x.TimestampUtc)
            })
            .ToList();

        var keys = grouped.Select(x => x.ErrorKey).ToList();
        var existing = await _dbContext.ApiErrorCatalog
            .Where(x => keys.Contains(x.ErrorKey))
            .ToDictionaryAsync(x => x.ErrorKey, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var affected = 0;
        foreach (var item in grouped)
        {
            if (existing.TryGetValue(item.ErrorKey, out var current))
            {
                current.ErrorType = item.ErrorType;
                current.NormalizedMessage = item.Message;
                current.FirstSeenUtc = item.FirstSeenUtc < current.FirstSeenUtc ? item.FirstSeenUtc : current.FirstSeenUtc;
                current.LastSeenUtc = item.LastSeenUtc > current.LastSeenUtc ? item.LastSeenUtc : current.LastSeenUtc;
                current.UpdatedAt = DateTime.UtcNow;
                affected++;
                continue;
            }

            _dbContext.ApiErrorCatalog.Add(new ApiErrorCatalog
            {
                ErrorKey = item.ErrorKey,
                ErrorType = item.ErrorType,
                NormalizedMessage = item.Message,
                FirstSeenUtc = item.FirstSeenUtc,
                LastSeenUtc = item.LastSeenUtc
            });
            affected++;
        }

        return affected;
    }

    private async Task<int> UpsertHourlyErrorOccurrencesAsync(
        IReadOnlyCollection<ApiRequestLog> errorLogs,
        DateTime hourlyFromUtc,
        CancellationToken cancellationToken)
    {
        await _dbContext.ApiErrorOccurrencesHourly
            .Where(x => x.BucketStartUtc >= hourlyFromUtc)
            .ExecuteDeleteAsync(cancellationToken);

        var prepared = errorLogs
            .Where(x => !string.IsNullOrWhiteSpace(x.NormalizedErrorKey))
            .ToList();

        if (prepared.Count == 0)
        {
            return 0;
        }

        var keys = prepared
            .Select(x => x.NormalizedErrorKey!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var catalogMap = await _dbContext.ApiErrorCatalog
            .Where(x => keys.Contains(x.ErrorKey))
            .ToDictionaryAsync(x => x.ErrorKey, x => x.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var rows = prepared
            .GroupBy(x => new
            {
                BucketStartUtc = TruncateToHour(x.TimestampUtc),
                ErrorKey = x.NormalizedErrorKey!,
                x.Method,
                x.EndpointTemplate,
                x.StatusCode,
                Severity = NormalizeSeverity(x.Severity),
                TenantId = NormalizeTenantId(x.TenantId)
            })
            .Select(g =>
            {
                if (!catalogMap.TryGetValue(g.Key.ErrorKey, out var catalogId))
                {
                    return null;
                }

                return new ApiErrorOccurrenceHourly
                {
                    ErrorCatalogId = catalogId,
                    BucketStartUtc = g.Key.BucketStartUtc,
                    Method = g.Key.Method,
                    EndpointTemplate = g.Key.EndpointTemplate,
                    StatusCode = g.Key.StatusCode,
                    Severity = g.Key.Severity,
                    TenantId = g.Key.TenantId,
                    OccurrenceCount = g.LongCount()
                };
            })
            .Where(x => x != null)
            .Cast<ApiErrorOccurrenceHourly>()
            .ToList();

        if (rows.Count > 0)
        {
            await _dbContext.ApiErrorOccurrencesHourly.AddRangeAsync(rows, cancellationToken);
        }

        return rows.Count;
    }

    private static List<ApiEndpointMetricHourly> BuildHourlyMetrics(IEnumerable<ApiRequestLog> logs)
    {
        return logs
            .GroupBy(x => new
            {
                BucketStartUtc = TruncateToHour(x.TimestampUtc),
                x.Method,
                x.EndpointTemplate,
                x.StatusCode,
                Severity = NormalizeSeverity(x.Severity),
                TenantId = NormalizeTenantId(x.TenantId)
            })
            .Select(g =>
            {
                var durations = g.Select(x => x.DurationMs).ToArray();
                return new ApiEndpointMetricHourly
                {
                    BucketStartUtc = g.Key.BucketStartUtc,
                    Method = g.Key.Method,
                    EndpointTemplate = g.Key.EndpointTemplate,
                    StatusCode = g.Key.StatusCode,
                    Severity = g.Key.Severity,
                    TenantId = g.Key.TenantId,
                    RequestCount = g.LongCount(),
                    ErrorCount = g.LongCount(x => IsError(x.StatusCode, x.IsError)),
                    WarningCount = g.Sum(x => (long)x.WarningCount),
                    TotalDurationMs = durations.Sum(x => (long)x),
                    MinDurationMs = durations.Length == 0 ? 0 : durations.Min(),
                    MaxDurationMs = durations.Length == 0 ? 0 : durations.Max(),
                    P50DurationMs = CalculatePercentile(durations, 0.50),
                    P95DurationMs = CalculatePercentile(durations, 0.95),
                    P99DurationMs = CalculatePercentile(durations, 0.99)
                };
            })
            .ToList();
    }

    private static List<ApiEndpointMetricDaily> BuildDailyMetrics(IEnumerable<ApiRequestLog> logs)
    {
        return logs
            .GroupBy(x => new
            {
                BucketDateUtc = TruncateToDay(x.TimestampUtc),
                x.Method,
                x.EndpointTemplate,
                x.StatusCode,
                Severity = NormalizeSeverity(x.Severity),
                TenantId = NormalizeTenantId(x.TenantId)
            })
            .Select(g =>
            {
                var durations = g.Select(x => x.DurationMs).ToArray();
                return new ApiEndpointMetricDaily
                {
                    BucketDateUtc = g.Key.BucketDateUtc,
                    Method = g.Key.Method,
                    EndpointTemplate = g.Key.EndpointTemplate,
                    StatusCode = g.Key.StatusCode,
                    Severity = g.Key.Severity,
                    TenantId = g.Key.TenantId,
                    RequestCount = g.LongCount(),
                    ErrorCount = g.LongCount(x => IsError(x.StatusCode, x.IsError)),
                    WarningCount = g.Sum(x => (long)x.WarningCount),
                    TotalDurationMs = durations.Sum(x => (long)x),
                    MinDurationMs = durations.Length == 0 ? 0 : durations.Min(),
                    MaxDurationMs = durations.Length == 0 ? 0 : durations.Max(),
                    P50DurationMs = CalculatePercentile(durations, 0.50),
                    P95DurationMs = CalculatePercentile(durations, 0.95),
                    P99DurationMs = CalculatePercentile(durations, 0.99)
                };
            })
            .ToList();
    }

    private static ApiRequestLog MapToEntity(ApiRequestTelemetryEventDto source)
    {
        return new ApiRequestLog
        {
            TimestampUtc = source.TimestampUtc,
            CorrelationId = source.CorrelationId,
            TraceId = source.TraceId,
            Method = source.Method,
            EndpointTemplate = source.EndpointTemplate,
            Path = source.Path,
            StatusCode = source.StatusCode,
            DurationMs = Math.Max(0, source.DurationMs),
            Severity = NormalizeSeverity(source.Severity),
            IsError = source.IsError,
            WarningCount = Math.Max(0, source.WarningCount),
            WarningCodesJson = source.WarningCodesJson,
            ErrorType = source.ErrorType,
            NormalizedErrorMessage = source.NormalizedErrorMessage,
            NormalizedErrorKey = source.NormalizedErrorKey,
            IpHash = source.IpHash,
            UserAgent = source.UserAgent,
            UserId = source.UserId,
            TenantId = source.TenantId,
            RequestSizeBytes = source.RequestSizeBytes,
            ResponseSizeBytes = source.ResponseSizeBytes,
            Scheme = source.Scheme,
            Host = source.Host,
            CreatedAt = source.TimestampUtc
        };
    }

    private static IQueryable<ApiRequestLog> ApplyFilters(
        IQueryable<ApiRequestLog> source,
        MonitoringRange range,
        string? endpoint,
        int? statusCode,
        Guid? userId,
        string? tenantId,
        string? severity)
    {
        var query = source
            .Where(x => x.TimestampUtc >= range.FromUtc && x.TimestampUtc <= range.ToUtc)
            // Evita distorcao por auto-monitoramento do proprio dashboard de monitoramento.
            .Where(x => !x.Path.StartsWith("/api/admin/monitoring"))
            // Exclui apenas o proprio hub de monitoramento para nao gerar feedback loop.
            .Where(x => !x.Path.StartsWith("/adminMonitoringHub"));

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            var normalizedEndpoint = endpoint.Trim();
            query = query.Where(x => x.EndpointTemplate.Contains(normalizedEndpoint));
        }

        if (statusCode.HasValue)
        {
            query = query.Where(x => x.StatusCode == statusCode.Value);
        }

        if (userId.HasValue)
        {
            query = query.Where(x => x.UserId == userId.Value);
        }

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            var normalizedTenant = tenantId.Trim();
            query = query.Where(x => x.TenantId == normalizedTenant);
        }

        if (!string.IsNullOrWhiteSpace(severity))
        {
            var normalizedSeverity = NormalizeSeverity(severity);
            query = query.Where(x => x.Severity == normalizedSeverity);
        }

        return query;
    }

    private static MonitoringRange ResolveRange(string? range)
    {
        var nowUtc = DateTime.UtcNow;
        return (range ?? "1h").Trim().ToLowerInvariant() switch
        {
            "1h" => new MonitoringRange(nowUtc.AddHours(-1), nowUtc),
            "2h" => new MonitoringRange(nowUtc.AddHours(-2), nowUtc),
            "4h" => new MonitoringRange(nowUtc.AddHours(-4), nowUtc),
            "6h" => new MonitoringRange(nowUtc.AddHours(-6), nowUtc),
            "8h" => new MonitoringRange(nowUtc.AddHours(-8), nowUtc),
            "12h" => new MonitoringRange(nowUtc.AddHours(-12), nowUtc),
            "24h" => new MonitoringRange(nowUtc.AddHours(-24), nowUtc),
            "7d" => new MonitoringRange(nowUtc.AddDays(-7), nowUtc),
            "30d" => new MonitoringRange(nowUtc.AddDays(-30), nowUtc),
            _ => new MonitoringRange(nowUtc.AddHours(-1), nowUtc)
        };
    }

    private static string NormalizeGroupBy(string? groupBy)
    {
        if (string.IsNullOrWhiteSpace(groupBy))
        {
            return "type";
        }

        var normalized = groupBy.Trim().ToLowerInvariant();
        return normalized is "endpoint" or "status" or "type" ? normalized : "type";
    }

    private static int ResolveBucketMinutes(MonitoringRange range)
    {
        if (range.Duration <= TimeSpan.FromHours(2))
        {
            return 5;
        }

        if (range.Duration <= TimeSpan.FromHours(6))
        {
            return 10;
        }

        if (range.Duration <= TimeSpan.FromHours(12))
        {
            return 15;
        }

        if (range.Duration <= TimeSpan.FromHours(30))
        {
            return 60;
        }

        if (range.Duration <= TimeSpan.FromDays(8))
        {
            return 180;
        }

        return 1440;
    }

    private static DateTime TruncateToHour(DateTime value)
    {
        return new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0, DateTimeKind.Utc);
    }

    private static DateTime TruncateToDay(DateTime value)
    {
        return new DateTime(value.Year, value.Month, value.Day, 0, 0, 0, DateTimeKind.Utc);
    }

    private static DateTime TruncateToBucket(DateTime value, int bucketMinutes)
    {
        if (bucketMinutes >= 1440)
        {
            return TruncateToDay(value);
        }

        var truncated = new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0, DateTimeKind.Utc);
        var minutesBucket = (value.Minute / bucketMinutes) * bucketMinutes;
        return truncated.AddMinutes(minutesBucket);
    }

    private static int CalculatePercentile(IEnumerable<int> values, double percentile)
    {
        var ordered = values.OrderBy(x => x).ToArray();
        if (ordered.Length == 0)
        {
            return 0;
        }

        var rank = (int)Math.Ceiling(percentile * ordered.Length) - 1;
        rank = Math.Clamp(rank, 0, ordered.Length - 1);
        return ordered[rank];
    }

    private static string NormalizeSeverity(string? severity)
    {
        if (string.IsNullOrWhiteSpace(severity))
        {
            return "info";
        }

        var normalized = severity.Trim().ToLowerInvariant();
        return normalized switch
        {
            "error" => "error",
            "warn" => "warn",
            "warning" => "warn",
            _ => "info"
        };
    }

    private static string NormalizeTenantId(string? tenantId)
    {
        return string.IsNullOrWhiteSpace(tenantId) ? string.Empty : tenantId.Trim();
    }

    private static bool IsError(int statusCode, bool isError)
    {
        return isError || statusCode >= 500;
    }

    private static double RoundPercent(long value, long total)
    {
        if (total <= 0)
        {
            return 0;
        }

        return Math.Round((double)value * 100d / total, 2);
    }

    private sealed record MonitoringRange(DateTime FromUtc, DateTime ToUtc)
    {
        public TimeSpan Duration => ToUtc - FromUtc;
    }

    private sealed record RequestProjection(
        DateTime TimestampUtc,
        string Method,
        string EndpointTemplate,
        int StatusCode,
        int DurationMs,
        string Severity,
        bool IsError,
        int WarningCount,
        string? NormalizedErrorKey,
        string? ErrorType,
        string? NormalizedErrorMessage);
}
