using System.Text.Json;
using ConsertaPraMim.Application.Constants;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConsertaPraMim.Infrastructure.Services;

public sealed class FireTvDashboardRuntimeSettings : IFireTvDashboardRuntimeSettings
{
    private const string CacheKey = "firetv.dashboard.runtime.config";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(15);

    private static readonly string[] DefaultKpiKeys =
    [
        "totalSessions",
        "uniqueVisitors",
        "leadSubmissions",
        "leadSubmissionRatePercent",
        "leadModalOpens",
        "totalClicks",
        "averageActiveSecondsPerSession",
        "averageMaxScrollPercent"
    ];

    private static readonly HashSet<string> AllowedKpiKeys = new(DefaultKpiKeys.Concat(["sessionsWithGeoRatePercent"]), StringComparer.OrdinalIgnoreCase);
    private static readonly FireTvDashboardFilterOptionConfigDto[] DefaultOriginFilters =
    [
        new("all", "Todas as origens"),
        new("client", "Cliente"),
        new("provider", "Prestador")
    ];

    private static readonly FireTvDashboardFilterOptionConfigDto[] DefaultComparisonModes =
    [
        new("none", "Sem comparacao"),
        new("previous_period", "Periodo anterior")
    ];

    private static readonly Dictionary<string, string> AllowedOriginFilterLabels = DefaultOriginFilters
        .ToDictionary(item => item.Value, item => item.Label, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> AllowedComparisonModeLabels = DefaultComparisonModes
        .ToDictionary(item => item.Value, item => item.Label, StringComparer.OrdinalIgnoreCase);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<FireTvDashboardRuntimeSettings> _logger;
    private readonly FireTvDashboardRuntimeConfigDto _fallbackConfig;

    public FireTvDashboardRuntimeSettings(
        IServiceScopeFactory scopeFactory,
        IMemoryCache memoryCache,
        ILogger<FireTvDashboardRuntimeSettings> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _memoryCache = memoryCache;
        _logger = logger;
        _fallbackConfig = Sanitize(configuration.GetSection("FireTvDashboard").Get<FireTvDashboardRuntimeConfigDto>() ?? new FireTvDashboardRuntimeConfigDto());
    }

    public async Task<FireTvDashboardRuntimeConfigDto> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(CacheKey, out FireTvDashboardRuntimeConfigDto? cached) && cached != null)
        {
            return cached;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ConsertaPraMimDbContext>();
            var setting = await dbContext.SystemSettings
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Key == SystemSettingKeys.ConfigFireTvDashboard, cancellationToken);

            if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
            {
                _memoryCache.Set(CacheKey, _fallbackConfig, CacheTtl);
                return _fallbackConfig;
            }

            var parsed = JsonSerializer.Deserialize<FireTvDashboardRuntimeConfigDto>(setting.Value);
            var sanitized = Sanitize(parsed ?? _fallbackConfig);
            _memoryCache.Set(CacheKey, sanitized, CacheTtl);
            return sanitized;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao carregar configuracao runtime do dashboard Fire TV. Usando fallback.");
            _memoryCache.Set(CacheKey, _fallbackConfig, CacheTtl);
            return _fallbackConfig;
        }
    }

    public void InvalidateCache()
    {
        _memoryCache.Remove(CacheKey);
    }

    private static FireTvDashboardRuntimeConfigDto Sanitize(FireTvDashboardRuntimeConfigDto raw)
    {
        var allowedRangeDays = (raw.AllowedRangeDays ?? Array.Empty<int>())
            .Select(value => Math.Clamp(value, 1, 90))
            .Distinct()
            .OrderBy(value => value)
            .ToArray();

        if (allowedRangeDays.Length == 0)
        {
            allowedRangeDays = [1, 7, 30];
        }

        var originFilters = SanitizeOptions(raw.OriginFilters, AllowedOriginFilterLabels, DefaultOriginFilters);
        var comparisonModes = SanitizeOptions(raw.ComparisonModes, AllowedComparisonModeLabels, DefaultComparisonModes);
        var defaultRangeDays = allowedRangeDays.Contains(raw.DefaultRangeDays)
            ? raw.DefaultRangeDays
            : allowedRangeDays[0];
        var defaultOriginFilter = ResolveDefaultOption(raw.DefaultOriginFilter, originFilters, DefaultOriginFilters[0].Value);
        var defaultComparisonMode = ResolveDefaultOption(raw.DefaultComparisonMode, comparisonModes, DefaultComparisonModes[1].Value);

        var kpiKeys = (raw.KpiKeys ?? Array.Empty<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Where(item => AllowedKpiKeys.Contains(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();

        if (kpiKeys.Length == 0)
        {
            kpiKeys = DefaultKpiKeys;
        }

        return new FireTvDashboardRuntimeConfigDto
        {
            Enabled = raw.Enabled,
            AppTitle = string.IsNullOrWhiteSpace(raw.AppTitle) ? "ConsertaPraMim Analytics TV" : raw.AppTitle.Trim(),
            AppSubtitle = string.IsNullOrWhiteSpace(raw.AppSubtitle) ? "Landing publica" : raw.AppSubtitle.Trim(),
            DefaultRangeDays = defaultRangeDays,
            AllowedRangeDays = allowedRangeDays,
            DefaultOriginFilter = defaultOriginFilter,
            OriginFilters = originFilters,
            DefaultComparisonMode = defaultComparisonMode,
            ComparisonModes = comparisonModes,
            AutoRefreshSeconds = Math.Clamp(raw.AutoRefreshSeconds, 10, 600),
            SessionPageSize = Math.Clamp(raw.SessionPageSize, 3, 12),
            TopListSize = Math.Clamp(raw.TopListSize, 3, 10),
            ShowHeatmap = raw.ShowHeatmap,
            ShowComparison = raw.ShowComparison,
            ShowScrollmap = raw.ShowScrollmap,
            ShowElementRanking = raw.ShowElementRanking,
            ElementRankingSize = Math.Clamp(raw.ElementRankingSize, 3, 12),
            KpiKeys = kpiKeys
        };
    }

    private static IReadOnlyList<FireTvDashboardFilterOptionConfigDto> SanitizeOptions(
        IReadOnlyList<FireTvDashboardFilterOptionConfigDto>? rawOptions,
        IReadOnlyDictionary<string, string> allowedValues,
        IReadOnlyList<FireTvDashboardFilterOptionConfigDto> fallbackOptions)
    {
        var sanitized = (rawOptions ?? Array.Empty<FireTvDashboardFilterOptionConfigDto>())
            .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Value))
            .Select(item =>
            {
                var value = item.Value.Trim();
                if (!allowedValues.ContainsKey(value))
                {
                    return null;
                }

                var label = string.IsNullOrWhiteSpace(item.Label)
                    ? allowedValues[value]
                    : item.Label.Trim();

                return new FireTvDashboardFilterOptionConfigDto(value, label);
            })
            .Where(item => item != null)
            .Cast<FireTvDashboardFilterOptionConfigDto>()
            .DistinctBy(item => item.Value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return sanitized.Length > 0 ? sanitized : fallbackOptions.ToArray();
    }

    private static string ResolveDefaultOption(
        string? rawValue,
        IReadOnlyList<FireTvDashboardFilterOptionConfigDto> allowedOptions,
        string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(rawValue) ? fallback : rawValue.Trim();
        return allowedOptions.Any(item => string.Equals(item.Value, normalized, StringComparison.OrdinalIgnoreCase))
            ? normalized
            : fallback;
    }
}
