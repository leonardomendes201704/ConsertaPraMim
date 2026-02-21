using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Constants;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ConsertaPraMim.Infrastructure.Services;

public class AdminMonitoringService : IAdminMonitoringService
{
    private readonly ConsertaPraMimDbContext _dbContext;
    private readonly ILogger<AdminMonitoringService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly IMonitoringRuntimeSettings _monitoringRuntimeSettings;
    private readonly ICorsRuntimeSettings _corsRuntimeSettings;
    private readonly IConfiguration _configuration;
    private readonly string? _fallbackClientPortalHealthUrl;
    private readonly string? _fallbackProviderPortalHealthUrl;
    private readonly TimeSpan _dependencyHealthCacheDuration;
    private readonly TimeSpan _dependencyHealthTimeout;
    private readonly string _environmentName;
    private const string PortalLinksHealthCacheKey = "monitoring:dependency-health:portal-links";
    private const string TelemetryEnabledSettingDescription = "Habilita ou desabilita a captura de telemetria de requests da API.";
    private const string CorsAllowedOriginsSettingDescription = "Define a lista de origins permitidas para CORS no backend da API.";

    public AdminMonitoringService(
        ConsertaPraMimDbContext dbContext,
        ILogger<AdminMonitoringService> logger,
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache,
        IMonitoringRuntimeSettings monitoringRuntimeSettings,
        ICorsRuntimeSettings corsRuntimeSettings,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        _dbContext = dbContext;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _memoryCache = memoryCache;
        _monitoringRuntimeSettings = monitoringRuntimeSettings;
        _corsRuntimeSettings = corsRuntimeSettings;
        _configuration = configuration;
        _fallbackClientPortalHealthUrl =
            NormalizeAbsoluteUrl(configuration["AdminPortals:ClientUrl"]) ??
            NormalizeAbsoluteUrl(configuration["Monitoring:DependencyHealth:ClientPortalUrl"]);
        _fallbackProviderPortalHealthUrl =
            NormalizeAbsoluteUrl(configuration["AdminPortals:ProviderUrl"]) ??
            NormalizeAbsoluteUrl(configuration["Monitoring:DependencyHealth:ProviderPortalUrl"]);

        var dependencyHealthCacheSeconds = Math.Clamp(
            configuration.GetValue<int?>("Monitoring:DependencyHealth:CacheSeconds") ?? 15,
            1,
            300);
        _dependencyHealthCacheDuration = TimeSpan.FromSeconds(dependencyHealthCacheSeconds);

        var dependencyHealthTimeoutMs = Math.Clamp(
            configuration.GetValue<int?>("Monitoring:DependencyHealth:TimeoutMs") ?? 3000,
            500,
            30000);
        _dependencyHealthTimeout = TimeSpan.FromMilliseconds(dependencyHealthTimeoutMs);

        _environmentName = string.IsNullOrWhiteSpace(hostEnvironment.EnvironmentName)
            ? "unknown"
            : hostEnvironment.EnvironmentName.Trim();
    }

    public async Task<AdminMonitoringRuntimeConfigDto> GetRuntimeConfigAsync(
        CancellationToken cancellationToken = default)
    {
        return await _monitoringRuntimeSettings.GetTelemetryConfigAsync(cancellationToken);
    }

    public async Task<AdminMonitoringRuntimeConfigDto> SetTelemetryEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var normalizedValue = enabled ? "true" : "false";

        var setting = await _dbContext.SystemSettings
            .SingleOrDefaultAsync(
                x => x.Key == SystemSettingKeys.MonitoringTelemetryEnabled,
                cancellationToken);

        if (setting == null)
        {
            setting = new SystemSetting
            {
                Key = SystemSettingKeys.MonitoringTelemetryEnabled,
                Value = normalizedValue,
                Description = TelemetryEnabledSettingDescription,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };

            await _dbContext.SystemSettings.AddAsync(setting, cancellationToken);
        }
        else
        {
            setting.Value = normalizedValue;
            setting.Description = string.IsNullOrWhiteSpace(setting.Description)
                ? TelemetryEnabledSettingDescription
                : setting.Description;
            setting.UpdatedAt = nowUtc;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _monitoringRuntimeSettings.InvalidateTelemetryCache();

        return new AdminMonitoringRuntimeConfigDto(
            TelemetryEnabled: enabled,
            UpdatedAtUtc: setting.UpdatedAt ?? setting.CreatedAt);
    }

    public async Task<AdminCorsRuntimeConfigDto> GetCorsConfigAsync(
        CancellationToken cancellationToken = default)
    {
        return await _corsRuntimeSettings.GetCorsConfigAsync(cancellationToken);
    }

    public async Task<AdminCorsRuntimeConfigDto> SetCorsConfigAsync(
        IReadOnlyCollection<string> allowedOrigins,
        CancellationToken cancellationToken = default)
    {
        var normalizedOrigins = NormalizeCorsOrigins(allowedOrigins);
        var serializedValue = JsonSerializer.Serialize(normalizedOrigins);
        var nowUtc = DateTime.UtcNow;

        var setting = await _dbContext.SystemSettings
            .SingleOrDefaultAsync(
                x => x.Key == SystemSettingKeys.CorsAllowedOrigins,
                cancellationToken);

        if (setting == null)
        {
            setting = new SystemSetting
            {
                Key = SystemSettingKeys.CorsAllowedOrigins,
                Value = serializedValue,
                Description = CorsAllowedOriginsSettingDescription,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };

            await _dbContext.SystemSettings.AddAsync(setting, cancellationToken);
        }
        else
        {
            setting.Value = serializedValue;
            setting.Description = string.IsNullOrWhiteSpace(setting.Description)
                ? CorsAllowedOriginsSettingDescription
                : setting.Description;
            setting.UpdatedAt = nowUtc;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _corsRuntimeSettings.InvalidateCorsCache();

        return new AdminCorsRuntimeConfigDto(
            AllowedOrigins: normalizedOrigins,
            UpdatedAtUtc: setting.UpdatedAt ?? setting.CreatedAt);
    }

    public async Task<AdminRuntimeConfigSectionsResponseDto> GetConfigSectionsAsync(
        CancellationToken cancellationToken = default)
    {
        var definitions = RuntimeConfigSections.All;
        var keys = definitions.Select(x => x.SettingKey).ToList();
        var nowUtc = DateTime.UtcNow;

        var settings = await _dbContext.SystemSettings
            .Where(x => keys.Contains(x.Key))
            .ToDictionaryAsync(x => x.Key, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var items = definitions
            .Select(definition =>
            {
                settings.TryGetValue(definition.SettingKey, out var setting);
                var jsonValue = setting != null && !string.IsNullOrWhiteSpace(setting.Value)
                    ? NormalizeJsonOrFallback(setting.Value, definition.DefaultJson)
                    : ResolveConfigSectionDefaultJson(definition);

                return new AdminRuntimeConfigSectionDto(
                    SettingKey: definition.SettingKey,
                    SectionPath: definition.SectionPath,
                    DisplayName: definition.DisplayName,
                    Description: definition.Description,
                    JsonValue: jsonValue,
                    UpdatedAtUtc: setting?.UpdatedAt ?? setting?.CreatedAt ?? nowUtc,
                    RequiresRestart: definition.RequiresRestart);
            })
            .ToList();

        return new AdminRuntimeConfigSectionsResponseDto(items);
    }

    public async Task<AdminRuntimeConfigSectionDto> SetConfigSectionAsync(
        string sectionPath,
        string jsonValue,
        CancellationToken cancellationToken = default)
    {
        if (!RuntimeConfigSections.TryGetBySectionPath(sectionPath, out var definition))
        {
            throw new ArgumentException($"Secao de configuracao nao suportada: {sectionPath}", nameof(sectionPath));
        }

        if (!TryNormalizeJsonDocument(jsonValue, out var normalizedJson, out var rootKind))
        {
            throw new ArgumentException("JsonValue invalido. Informe um JSON valido.", nameof(jsonValue));
        }

        if (rootKind != JsonValueKind.Object)
        {
            throw new ArgumentException("JsonValue deve ser um objeto JSON de configuracao.", nameof(jsonValue));
        }

        var nowUtc = DateTime.UtcNow;
        var setting = await _dbContext.SystemSettings
            .SingleOrDefaultAsync(x => x.Key == definition.SettingKey, cancellationToken);

        if (setting == null)
        {
            setting = new SystemSetting
            {
                Key = definition.SettingKey,
                Value = normalizedJson,
                Description = definition.Description,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };

            await _dbContext.SystemSettings.AddAsync(setting, cancellationToken);
        }
        else
        {
            setting.Value = normalizedJson;
            setting.Description = string.IsNullOrWhiteSpace(setting.Description)
                ? definition.Description
                : setting.Description;
            setting.UpdatedAt = nowUtc;
        }

        if (definition.SettingKey == SystemSettingKeys.ConfigMonitoring &&
            TryExtractMonitoringEnabled(normalizedJson, out var monitoringEnabled))
        {
            var telemetrySetting = await _dbContext.SystemSettings
                .SingleOrDefaultAsync(
                    x => x.Key == SystemSettingKeys.MonitoringTelemetryEnabled,
                    cancellationToken);

            var telemetryValue = monitoringEnabled ? "true" : "false";
            if (telemetrySetting == null)
            {
                telemetrySetting = new SystemSetting
                {
                    Key = SystemSettingKeys.MonitoringTelemetryEnabled,
                    Value = telemetryValue,
                    Description = TelemetryEnabledSettingDescription,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };
                await _dbContext.SystemSettings.AddAsync(telemetrySetting, cancellationToken);
            }
            else
            {
                telemetrySetting.Value = telemetryValue;
                telemetrySetting.Description = string.IsNullOrWhiteSpace(telemetrySetting.Description)
                    ? TelemetryEnabledSettingDescription
                    : telemetrySetting.Description;
                telemetrySetting.UpdatedAt = nowUtc;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _monitoringRuntimeSettings.InvalidateTelemetryCache();
        _corsRuntimeSettings.InvalidateCorsCache();

        return new AdminRuntimeConfigSectionDto(
            SettingKey: definition.SettingKey,
            SectionPath: definition.SectionPath,
            DisplayName: definition.DisplayName,
            Description: definition.Description,
            JsonValue: normalizedJson,
            UpdatedAtUtc: setting.UpdatedAt ?? setting.CreatedAt,
            RequiresRestart: definition.RequiresRestart);
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
        const string apiHealthStatus = "healthy";
        var apiUptimeSeconds = ResolveApiUptimeSeconds();
        var databaseHealthStatus = await ResolveDatabaseHealthStatusAsync(cancellationToken);
        var portalHealthTargets = await ResolvePortalHealthTargetsAsync(cancellationToken);
        var clientPortalHealthStatus = await ResolveDependencyHealthStatusAsync(
            "web-client",
            portalHealthTargets.ClientPortalUrl,
            cancellationToken);
        var providerPortalHealthStatus = await ResolveDependencyHealthStatusAsync(
            "web-provider",
            portalHealthTargets.ProviderPortalUrl,
            cancellationToken);

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
                TopErrors: [],
                ApiUptimeSeconds: apiUptimeSeconds,
                ApiHealthStatus: apiHealthStatus,
                DatabaseHealthStatus: databaseHealthStatus,
                ClientPortalHealthStatus: clientPortalHealthStatus,
                ProviderPortalHealthStatus: providerPortalHealthStatus);
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

        var errorCountByBucket = logs
            .Where(x => x.StatusCode >= 400 || IsError(x.StatusCode, x.IsError))
            .GroupBy(x => TruncateToBucket(x.TimestampUtc, bucketMinutes))
            .ToDictionary(g => g.Key, g => g.LongCount());

        // Keep the same timeline buckets as requestSeries so chart never renders blank
        // when there are requests but zero errors in the selected interval.
        var errorSeries = requestSeries
            .Select(point => new AdminMonitoringTimeseriesPointDto(
                point.BucketUtc,
                errorCountByBucket.TryGetValue(point.BucketUtc, out var count) ? count : 0))
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
            TopErrors: topErrors,
            ApiUptimeSeconds: apiUptimeSeconds,
            ApiHealthStatus: apiHealthStatus,
            DatabaseHealthStatus: databaseHealthStatus,
            ClientPortalHealthStatus: clientPortalHealthStatus,
            ProviderPortalHealthStatus: providerPortalHealthStatus);
    }

    private async Task<string> ResolveDatabaseHealthStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _dbContext.Database.CanConnectAsync(cancellationToken)
                ? "healthy"
                : "unhealthy";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to validate database connectivity for monitoring overview.");
            return "unknown";
        }
    }

    private async Task<PortalHealthTargets> ResolvePortalHealthTargetsAsync(CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue(PortalLinksHealthCacheKey, out PortalHealthTargets? cachedTargets) &&
            cachedTargets != null)
        {
            return cachedTargets;
        }

        var fallbackTargets = new PortalHealthTargets(
            ClientPortalUrl: _fallbackClientPortalHealthUrl,
            ProviderPortalUrl: _fallbackProviderPortalHealthUrl);

        try
        {
            var setting = await _dbContext.SystemSettings
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Key == SystemSettingKeys.ConfigAdminPortals,
                    cancellationToken);

            var resolvedTargets = TryParsePortalHealthTargetsFromJson(setting?.Value, fallbackTargets);
            _memoryCache.Set(PortalLinksHealthCacheKey, resolvedTargets, _dependencyHealthCacheDuration);
            return resolvedTargets;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _memoryCache.Set(PortalLinksHealthCacheKey, fallbackTargets, TimeSpan.FromSeconds(3));
            return fallbackTargets;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to resolve portal health targets from SystemSettings.");
            _memoryCache.Set(PortalLinksHealthCacheKey, fallbackTargets, TimeSpan.FromSeconds(3));
            return fallbackTargets;
        }
    }

    private static PortalHealthTargets TryParsePortalHealthTargetsFromJson(
        string? rawJson,
        PortalHealthTargets fallbackTargets)
    {
        if (!TryNormalizeJsonDocument(rawJson, out var normalizedJson, out var rootKind) ||
            rootKind != JsonValueKind.Object)
        {
            return fallbackTargets;
        }

        try
        {
            using var document = JsonDocument.Parse(normalizedJson);
            var root = document.RootElement;

            var clientPortalUrl = TryGetJsonStringProperty(root, "ClientUrl");
            var providerPortalUrl = TryGetJsonStringProperty(root, "ProviderUrl");

            return new PortalHealthTargets(
                ClientPortalUrl: NormalizeAbsoluteUrl(clientPortalUrl) ?? fallbackTargets.ClientPortalUrl,
                ProviderPortalUrl: NormalizeAbsoluteUrl(providerPortalUrl) ?? fallbackTargets.ProviderPortalUrl);
        }
        catch
        {
            return fallbackTargets;
        }
    }

    private async Task<string> ResolveDependencyHealthStatusAsync(
        string dependencyName,
        string? dependencyUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dependencyUrl))
        {
            return "unknown";
        }

        var cacheKey = $"monitoring:dependency-health:{dependencyName}:{dependencyUrl}";
        if (_memoryCache.TryGetValue(cacheKey, out string? cachedStatus) &&
            !string.IsNullOrWhiteSpace(cachedStatus))
        {
            return cachedStatus;
        }

        var status = await ProbeDependencyHealthAsync(dependencyName, dependencyUrl, cancellationToken);
        _memoryCache.Set(cacheKey, status, _dependencyHealthCacheDuration);
        return status;
    }

    private async Task<string> ProbeDependencyHealthAsync(
        string dependencyName,
        string dependencyUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_dependencyHealthTimeout);

            using var request = new HttpRequestMessage(HttpMethod.Get, dependencyUrl);
            using var client = _httpClientFactory.CreateClient();
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token);

            return (int)response.StatusCode >= 500
                ? "unhealthy"
                : "healthy";
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return "unhealthy";
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Unable to probe dependency health for {DependencyName} at {DependencyUrl}.",
                dependencyName,
                dependencyUrl);
            return "unhealthy";
        }
    }

    private static long ResolveApiUptimeSeconds()
    {
        try
        {
            var processStartUtc = Process.GetCurrentProcess().StartTime.ToUniversalTime();
            var uptime = DateTime.UtcNow - processStartUtc;
            return uptime < TimeSpan.Zero
                ? 0
                : (long)Math.Floor(uptime.TotalSeconds);
        }
        catch
        {
            return 0;
        }
    }

    private static string? NormalizeAbsoluteUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            ? uri.ToString()
            : null;
    }

    private static string? TryGetJsonStringProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(propertyName))
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Null => null,
                _ => property.Value.GetRawText()
            };
        }

        return null;
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
            // Keep Top Errors aligned with overview error-series semantics:
            // 4xx and 5xx are both considered error buckets for observability.
            // Use only SQL-translatable predicates here.
            .Where(x => x.StatusCode >= 400 || x.IsError)
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

    public async Task<AdminMonitoringErrorDetailsDto?> GetErrorDetailsAsync(
        AdminMonitoringErrorDetailsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.ErrorKey))
        {
            return null;
        }

        var range = ResolveRange(query.Range);
        var groupBy = NormalizeGroupBy(query.GroupBy);
        var normalizedErrorKey = query.ErrorKey.Trim();
        var take = Math.Clamp(query.Take, 1, 25);

        var filteredErrors = ApplyFilters(
                _dbContext.ApiRequestLogs.AsNoTracking(),
                range,
                query.Endpoint,
                query.StatusCode,
                query.UserId,
                query.TenantId,
                query.Severity)
            .Where(x => x.StatusCode >= 400 || x.IsError);

        IQueryable<ApiRequestLog> matchingQuery = groupBy switch
        {
            "endpoint" => filteredErrors.Where(x => x.EndpointTemplate == normalizedErrorKey),
            "status" => TryParseStatusCode(normalizedErrorKey, out var statusCode)
                ? filteredErrors.Where(x => x.StatusCode == statusCode)
                : filteredErrors.Where(_ => false),
            _ => ApplyTypeErrorKeyFilter(filteredErrors, normalizedErrorKey)
        };

        var summary = await matchingQuery
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.LongCount(),
                FirstSeenUtc = g.Min(x => x.TimestampUtc),
                LastSeenUtc = g.Max(x => x.TimestampUtc)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (summary == null || summary.Count == 0)
        {
            return null;
        }

        var sampleEntities = await matchingQuery
            .OrderByDescending(x => x.TimestampUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

        if (sampleEntities.Count == 0)
        {
            return null;
        }

        var firstSample = sampleEntities[0];
        var endpointTemplate = firstSample.EndpointTemplate;
        var statusCodeValue = (int?)firstSample.StatusCode;
        var errorType = firstSample.ErrorType ?? "UnknownError";
        var message = firstSample.NormalizedErrorMessage ?? "Erro sem mensagem normalizada";

        switch (groupBy)
        {
            case "endpoint":
                errorType = "Endpoint";
                message = $"Erros concentrados em {normalizedErrorKey}";
                endpointTemplate = normalizedErrorKey;
                statusCodeValue = sampleEntities
                    .GroupBy(x => x.StatusCode)
                    .OrderByDescending(g => g.LongCount())
                    .Select(g => (int?)g.Key)
                    .FirstOrDefault();
                break;
            case "status":
                errorType = "StatusCode";
                message = $"Erros com status {normalizedErrorKey}";
                statusCodeValue = TryParseStatusCode(normalizedErrorKey, out var parsedStatusCode)
                    ? parsedStatusCode
                    : firstSample.StatusCode;
                endpointTemplate = sampleEntities
                    .GroupBy(x => x.EndpointTemplate)
                    .OrderByDescending(g => g.LongCount())
                    .Select(g => g.Key)
                    .FirstOrDefault();
                break;
            default:
                endpointTemplate = sampleEntities
                    .GroupBy(x => x.EndpointTemplate)
                    .OrderByDescending(g => g.LongCount())
                    .Select(g => g.Key)
                    .FirstOrDefault();
                statusCodeValue = sampleEntities
                    .GroupBy(x => x.StatusCode)
                    .OrderByDescending(g => g.LongCount())
                    .Select(g => (int?)g.Key)
                    .FirstOrDefault();
                if (string.IsNullOrWhiteSpace(errorType) &&
                    TryParseSyntheticTypeErrorKey(normalizedErrorKey, out var syntheticType, out _))
                {
                    errorType = syntheticType;
                }
                break;
        }

        var sampleStackTrace = sampleEntities
            .Select(x => TryExtractStackTrace(x.ResponseBodyJson, x.NormalizedErrorMessage))
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        var samples = sampleEntities
            .Select(MapRequestDetailsDto)
            .ToList();

        return new AdminMonitoringErrorDetailsDto(
            GroupBy: groupBy,
            ErrorKey: normalizedErrorKey,
            ErrorType: errorType,
            Message: message,
            Count: summary.Count,
            FirstSeenUtc: summary.FirstSeenUtc,
            LastSeenUtc: summary.LastSeenUtc,
            EndpointTemplate: endpointTemplate,
            StatusCode: statusCodeValue,
            SampleStackTrace: sampleStackTrace,
            Samples: samples);
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

        filtered = ApplySearchFilter(filtered, query.Search);

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
                x.TenantId,
                x.Scheme,
                x.Host,
                _environmentName))
            .ToListAsync(cancellationToken);

        return new AdminMonitoringRequestsResponseDto(
            Page: page,
            PageSize: pageSize,
            Total: total,
            Items: items);
    }

    public async Task<AdminMonitoringRequestsExportResponseDto> ExportRequestsCsvBase64Async(
        AdminMonitoringRequestsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var range = ResolveRange(query.Range);
        var filtered = ApplyFilters(
            _dbContext.ApiRequestLogs.AsNoTracking(),
            range,
            query.Endpoint,
            query.StatusCode,
            query.UserId,
            query.TenantId,
            query.Severity);

        filtered = ApplySearchFilter(filtered, query.Search);

        var rows = await filtered
            .OrderByDescending(x => x.TimestampUtc)
            .Select(x => new MonitoringCsvRow(
                x.TimestampUtc,
                x.CorrelationId,
                x.TraceId,
                x.Method,
                x.EndpointTemplate,
                x.Path,
                x.StatusCode,
                x.DurationMs,
                x.Severity,
                x.IsError,
                x.WarningCount,
                x.ErrorType,
                x.NormalizedErrorMessage,
                x.NormalizedErrorKey,
                x.UserId,
                x.TenantId,
                x.RequestSizeBytes,
                x.ResponseSizeBytes,
                x.Scheme,
                x.Host))
            .ToListAsync(cancellationToken);

        var csvContent = BuildRequestsCsv(rows);
        var fileName = $"admin-monitoring-requests-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

        return new AdminMonitoringRequestsExportResponseDto(
            FileName: fileName,
            ContentType: "text/csv; charset=utf-8",
            Base64Content: Convert.ToBase64String(Encoding.UTF8.GetBytes(csvContent)),
            TotalRows: rows.Count);
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

        return MapRequestDetailsDto(entity);
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
                Method = NormalizeHttpMethod(x.Method),
                EndpointTemplate = NormalizeEndpointTemplate(x.EndpointTemplate),
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
                Method = NormalizeHttpMethod(x.Method),
                EndpointTemplate = NormalizeEndpointTemplate(x.EndpointTemplate),
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
                Method = NormalizeHttpMethod(x.Method),
                EndpointTemplate = NormalizeEndpointTemplate(x.EndpointTemplate),
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
            Method = NormalizeHttpMethod(source.Method),
            EndpointTemplate = NormalizeEndpointTemplate(source.EndpointTemplate),
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
            TenantId = NormalizeTenantId(source.TenantId),
            RequestSizeBytes = source.RequestSizeBytes,
            ResponseSizeBytes = source.ResponseSizeBytes,
            Scheme = source.Scheme,
            Host = source.Host,
            RequestBodyJson = source.RequestBodyJson,
            ResponseBodyJson = source.ResponseBodyJson,
            RequestHeadersJson = source.RequestHeadersJson,
            QueryStringJson = source.QueryStringJson,
            RouteValuesJson = source.RouteValuesJson,
            CreatedAt = source.TimestampUtc
        };
    }

    private static IQueryable<ApiRequestLog> ApplySearchFilter(
        IQueryable<ApiRequestLog> source,
        string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return source;
        }

        var normalizedSearch = search.Trim();
        return source.Where(x =>
            x.CorrelationId.Contains(normalizedSearch) ||
            x.Method.Contains(normalizedSearch) ||
            x.EndpointTemplate.Contains(normalizedSearch) ||
            (x.ErrorType != null && x.ErrorType.Contains(normalizedSearch)) ||
            (x.NormalizedErrorMessage != null && x.NormalizedErrorMessage.Contains(normalizedSearch)) ||
            x.Path.Contains(normalizedSearch));
    }

    private static string BuildRequestsCsv(IReadOnlyCollection<MonitoringCsvRow> rows)
    {
        var builder = new StringBuilder(capacity: Math.Max(4096, rows.Count * 256));
        builder.Append('\uFEFF');
        builder.AppendLine("timestampUtc;correlationId;traceId;method;endpointTemplate;path;statusCode;durationMs;severity;isError;warningCount;errorType;normalizedErrorMessage;normalizedErrorKey;userId;tenantId;requestSizeBytes;responseSizeBytes;scheme;host");

        foreach (var row in rows)
        {
            builder.Append(EscapeCsv(row.TimestampUtc.ToString("o", CultureInfo.InvariantCulture))).Append(';')
                .Append(EscapeCsv(row.CorrelationId)).Append(';')
                .Append(EscapeCsv(row.TraceId)).Append(';')
                .Append(EscapeCsv(row.Method)).Append(';')
                .Append(EscapeCsv(row.EndpointTemplate)).Append(';')
                .Append(EscapeCsv(row.Path)).Append(';')
                .Append(EscapeCsv(row.StatusCode.ToString(CultureInfo.InvariantCulture))).Append(';')
                .Append(EscapeCsv(row.DurationMs.ToString(CultureInfo.InvariantCulture))).Append(';')
                .Append(EscapeCsv(row.Severity)).Append(';')
                .Append(EscapeCsv(row.IsError.ToString())).Append(';')
                .Append(EscapeCsv(row.WarningCount.ToString(CultureInfo.InvariantCulture))).Append(';')
                .Append(EscapeCsv(row.ErrorType)).Append(';')
                .Append(EscapeCsv(row.NormalizedErrorMessage)).Append(';')
                .Append(EscapeCsv(row.NormalizedErrorKey)).Append(';')
                .Append(EscapeCsv(row.UserId?.ToString("D", CultureInfo.InvariantCulture))).Append(';')
                .Append(EscapeCsv(row.TenantId)).Append(';')
                .Append(EscapeCsv(row.RequestSizeBytes?.ToString(CultureInfo.InvariantCulture))).Append(';')
                .Append(EscapeCsv(row.ResponseSizeBytes?.ToString(CultureInfo.InvariantCulture))).Append(';')
                .Append(EscapeCsv(row.Scheme)).Append(';')
                .Append(EscapeCsv(row.Host))
                .AppendLine();
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuotes = value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        if (!needsQuotes)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
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

    private static IQueryable<ApiRequestLog> ApplyTypeErrorKeyFilter(
        IQueryable<ApiRequestLog> source,
        string normalizedErrorKey)
    {
        var query = source.Where(x => x.NormalizedErrorKey == normalizedErrorKey);
        if (TryParseSyntheticTypeErrorKey(normalizedErrorKey, out var syntheticType, out var syntheticStatusCode))
        {
            query = query.Concat(
                source.Where(x =>
                    x.NormalizedErrorKey == null &&
                    x.ErrorType == syntheticType &&
                    x.StatusCode == syntheticStatusCode));
        }

        return query;
    }

    private static bool TryParseStatusCode(string? raw, out int statusCode)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            statusCode = 0;
            return false;
        }

        return int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out statusCode);
    }

    private static bool TryParseSyntheticTypeErrorKey(
        string? errorKey,
        out string errorType,
        out int statusCode)
    {
        errorType = string.Empty;
        statusCode = 0;

        if (string.IsNullOrWhiteSpace(errorKey))
        {
            return false;
        }

        var normalized = errorKey.Trim();
        var separatorIndex = normalized.LastIndexOf('|');
        if (separatorIndex <= 0 || separatorIndex >= normalized.Length - 1)
        {
            return false;
        }

        var candidateType = normalized[..separatorIndex].Trim();
        var statusToken = normalized[(separatorIndex + 1)..].Trim();

        if (string.IsNullOrWhiteSpace(candidateType) ||
            !int.TryParse(statusToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedStatus))
        {
            return false;
        }

        errorType = candidateType;
        statusCode = parsedStatus;
        return true;
    }

    private static string? TryExtractStackTrace(
        string? responseBodyJson,
        string? normalizedErrorMessage)
    {
        var fromResponseBody = TryExtractStackTraceFromRaw(responseBodyJson);
        if (!string.IsNullOrWhiteSpace(fromResponseBody))
        {
            return NormalizeStackTrace(fromResponseBody);
        }

        var fromErrorMessage = TryExtractStackTraceFromRaw(normalizedErrorMessage);
        return string.IsNullOrWhiteSpace(fromErrorMessage)
            ? null
            : NormalizeStackTrace(fromErrorMessage);
    }

    private static string? TryExtractStackTraceFromRaw(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        var extractedFromJson = TryExtractStackTraceFromJson(trimmed);
        if (!string.IsNullOrWhiteSpace(extractedFromJson))
        {
            return extractedFromJson;
        }

        if (trimmed.Contains(" at ", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("stacktrace", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return null;
    }

    private static string? TryExtractStackTraceFromJson(string raw)
    {
        try
        {
            using var document = JsonDocument.Parse(raw);
            return TryExtractStackTraceFromElement(document.RootElement);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryExtractStackTraceFromElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var preferredProperties = new[]
                {
                    "stackTrace",
                    "stacktrace",
                    "stack",
                    "exception",
                    "detail",
                    "details"
                };

                foreach (var propertyName in preferredProperties)
                {
                    if (!element.TryGetProperty(propertyName, out var property))
                    {
                        continue;
                    }

                    var extracted = TryExtractStackTraceFromElement(property);
                    if (!string.IsNullOrWhiteSpace(extracted))
                    {
                        return extracted;
                    }
                }

                foreach (var property in element.EnumerateObject())
                {
                    var extracted = TryExtractStackTraceFromElement(property.Value);
                    if (!string.IsNullOrWhiteSpace(extracted))
                    {
                        return extracted;
                    }
                }

                return null;
            }
            case JsonValueKind.Array:
            {
                foreach (var item in element.EnumerateArray())
                {
                    var extracted = TryExtractStackTraceFromElement(item);
                    if (!string.IsNullOrWhiteSpace(extracted))
                    {
                        return extracted;
                    }
                }

                return null;
            }
            case JsonValueKind.String:
            {
                var text = element.GetString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                if (text.Contains(" at ", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("stacktrace", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("exception", StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }

                return null;
            }
            default:
                return null;
        }
    }

    private static string NormalizeStackTrace(string stackTrace)
    {
        var normalized = stackTrace
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Trim();

        const int maxChars = 16000;
        if (normalized.Length <= maxChars)
        {
            return normalized;
        }

        return normalized[..maxChars] + "\n...[stack trace truncado]";
    }

    private AdminMonitoringRequestDetailsDto MapRequestDetailsDto(ApiRequestLog entity)
    {
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
            entity.Host,
            entity.RequestBodyJson,
            entity.ResponseBodyJson,
            _environmentName,
            entity.RequestHeadersJson,
            entity.QueryStringJson,
            entity.RouteValuesJson);
    }

    private string ResolveConfigSectionDefaultJson(RuntimeConfigSectionDefinition definition)
    {
        var configuredSectionJson = SerializeConfigurationSection(_configuration, definition.SectionPath);
        if (!string.IsNullOrWhiteSpace(configuredSectionJson))
        {
            return configuredSectionJson;
        }

        return NormalizeJsonOrFallback(definition.DefaultJson, "{}");
    }

    private static string? SerializeConfigurationSection(
        IConfiguration configuration,
        string sectionPath)
    {
        if (string.IsNullOrWhiteSpace(sectionPath))
        {
            return null;
        }

        var section = configuration.GetSection(sectionPath);
        if (!section.Exists())
        {
            return null;
        }

        var node = BuildConfigurationNode(section);
        if (node == null)
        {
            return null;
        }

        return JsonSerializer.Serialize(node);
    }

    private static object? BuildConfigurationNode(IConfigurationSection section)
    {
        var children = section.GetChildren().ToList();
        if (children.Count == 0)
        {
            return ParseConfigurationScalar(section.Value);
        }

        var isArray = children.All(child => int.TryParse(child.Key, out _));
        if (isArray)
        {
            var indexedChildren = children
                .Select(child => new
                {
                    Index = int.Parse(child.Key, CultureInfo.InvariantCulture),
                    Value = BuildConfigurationNode(child)
                })
                .ToList();

            if (indexedChildren.Count == 0)
            {
                return Array.Empty<object?>();
            }

            var maxIndex = indexedChildren.Max(x => x.Index);
            var array = new object?[maxIndex + 1];
            foreach (var item in indexedChildren)
            {
                array[item.Index] = item.Value;
            }

            return array;
        }

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in children)
        {
            result[child.Key] = BuildConfigurationNode(child);
        }

        return result;
    }

    private static object? ParseConfigurationScalar(string? rawValue)
    {
        if (rawValue == null)
        {
            return null;
        }

        var value = rawValue.Trim();
        if (value.Length == 0)
        {
            return string.Empty;
        }

        if (bool.TryParse(value, out var boolValue))
        {
            return boolValue;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            return longValue;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
        {
            return doubleValue;
        }

        return rawValue;
    }

    private static bool TryNormalizeJsonDocument(
        string? rawJson,
        out string normalizedJson,
        out JsonValueKind rootKind)
    {
        normalizedJson = string.Empty;
        rootKind = JsonValueKind.Undefined;

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            rootKind = document.RootElement.ValueKind;
            normalizedJson = JsonSerializer.Serialize(document.RootElement);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeJsonOrFallback(
        string? rawJson,
        string fallbackJson)
    {
        return TryNormalizeJsonDocument(rawJson, out var normalizedJson, out _)
            ? normalizedJson
            : fallbackJson;
    }

    private static bool TryExtractMonitoringEnabled(string monitoringJson, out bool enabled)
    {
        enabled = false;
        try
        {
            using var document = JsonDocument.Parse(monitoringJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!document.RootElement.TryGetProperty("Enabled", out var enabledProperty))
            {
                return false;
            }

            if (enabledProperty.ValueKind == JsonValueKind.True)
            {
                enabled = true;
                return true;
            }

            if (enabledProperty.ValueKind == JsonValueKind.False)
            {
                enabled = false;
                return true;
            }

            if (enabledProperty.ValueKind == JsonValueKind.String &&
                bool.TryParse(enabledProperty.GetString(), out var parsed))
            {
                enabled = parsed;
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static IReadOnlyList<string> NormalizeCorsOrigins(IEnumerable<string>? allowedOrigins)
    {
        if (allowedOrigins == null)
        {
            return [];
        }

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var origin in allowedOrigins)
        {
            var normalizedOrigin = NormalizeCorsOrigin(origin);
            if (normalizedOrigin == null || !seen.Add(normalizedOrigin))
            {
                continue;
            }

            result.Add(normalizedOrigin);
        }

        return result;
    }

    private static string? NormalizeCorsOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return null;
        }

        if (!Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        var normalized = uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped).TrimEnd('/');
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized.ToLowerInvariant();
    }

    private static string NormalizeHttpMethod(string? method)
    {
        return string.IsNullOrWhiteSpace(method)
            ? "GET"
            : method.Trim().ToUpperInvariant();
    }

    private static string NormalizeEndpointTemplate(string? endpointTemplate)
    {
        if (string.IsNullOrWhiteSpace(endpointTemplate))
        {
            return "/";
        }

        return endpointTemplate.Trim().ToLowerInvariant();
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

    private sealed record PortalHealthTargets(
        string? ClientPortalUrl,
        string? ProviderPortalUrl);

    private sealed record MonitoringCsvRow(
        DateTime TimestampUtc,
        string CorrelationId,
        string TraceId,
        string Method,
        string EndpointTemplate,
        string Path,
        int StatusCode,
        int DurationMs,
        string Severity,
        bool IsError,
        int WarningCount,
        string? ErrorType,
        string? NormalizedErrorMessage,
        string? NormalizedErrorKey,
        Guid? UserId,
        string? TenantId,
        long? RequestSizeBytes,
        long? ResponseSizeBytes,
        string Scheme,
        string? Host);

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
