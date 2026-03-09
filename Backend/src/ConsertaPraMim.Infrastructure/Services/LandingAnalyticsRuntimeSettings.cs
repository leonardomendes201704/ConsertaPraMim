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

public sealed class LandingAnalyticsRuntimeSettings : ILandingAnalyticsRuntimeSettings
{
    private const string CacheKey = "landing.analytics.runtime.config";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<LandingAnalyticsRuntimeSettings> _logger;
    private readonly LandingAnalyticsRuntimeConfigDto _fallbackConfig;

    public LandingAnalyticsRuntimeSettings(
        IServiceScopeFactory scopeFactory,
        IMemoryCache memoryCache,
        ILogger<LandingAnalyticsRuntimeSettings> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _memoryCache = memoryCache;
        _logger = logger;
        _fallbackConfig = Sanitize(configuration.GetSection("LandingAnalytics").Get<LandingAnalyticsRuntimeConfigDto>() ?? new LandingAnalyticsRuntimeConfigDto());
    }

    public async Task<LandingAnalyticsRuntimeConfigDto> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(CacheKey, out LandingAnalyticsRuntimeConfigDto? cached) && cached != null)
        {
            return cached;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ConsertaPraMimDbContext>();
            var setting = await dbContext.SystemSettings
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.Key == SystemSettingKeys.ConfigLandingAnalytics,
                    cancellationToken);

            if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
            {
                _memoryCache.Set(CacheKey, _fallbackConfig, CacheTtl);
                return _fallbackConfig;
            }

            var parsed = JsonSerializer.Deserialize<LandingAnalyticsRuntimeConfigDto>(setting.Value);
            var sanitized = Sanitize(parsed ?? _fallbackConfig);
            _memoryCache.Set(CacheKey, sanitized, CacheTtl);
            return sanitized;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao carregar configuracao runtime de Landing Analytics. Usando fallback.");
            _memoryCache.Set(CacheKey, _fallbackConfig, CacheTtl);
            return _fallbackConfig;
        }
    }

    public async Task<LandingAnalyticsPublicConfigDto> GetPublicConfigAsync(CancellationToken cancellationToken = default)
    {
        var config = await GetConfigAsync(cancellationToken);
        return new LandingAnalyticsPublicConfigDto
        {
            Enabled = config.ClientTelemetryEnabled,
            Heartbeat = config.Heartbeat,
            Scroll = config.Scroll,
            Clicks = config.Clicks
        };
    }

    public void InvalidateCache()
    {
        _memoryCache.Remove(CacheKey);
    }

    private static LandingAnalyticsRuntimeConfigDto Sanitize(LandingAnalyticsRuntimeConfigDto raw)
    {
        var milestones = (raw.Scroll?.MilestonesPercent ?? Array.Empty<int>())
            .Select(value => Math.Clamp(value, 1, 100))
            .Distinct()
            .OrderBy(value => value)
            .ToArray();

        if (milestones.Length == 0)
        {
            milestones = [25, 50, 75, 100];
        }

        return new LandingAnalyticsRuntimeConfigDto
        {
            ClientTelemetryEnabled = raw.ClientTelemetryEnabled,
            Heartbeat = new LandingHeartbeatRuntimeConfigDto
            {
                Enabled = raw.Heartbeat?.Enabled ?? true,
                IntervalSeconds = Math.Clamp(raw.Heartbeat?.IntervalSeconds ?? 15, 5, 120),
                MaxSessionDurationMinutes = Math.Clamp(raw.Heartbeat?.MaxSessionDurationMinutes ?? 30, 5, 240)
            },
            Scroll = new LandingScrollRuntimeConfigDto
            {
                Enabled = raw.Scroll?.Enabled ?? true,
                MilestonesPercent = milestones
            },
            Clicks = new LandingClicksRuntimeConfigDto
            {
                Enabled = raw.Clicks?.Enabled ?? true,
                TrackInteractiveOnly = raw.Clicks?.TrackInteractiveOnly ?? true,
                HeatmapGridRows = Math.Clamp(raw.Clicks?.HeatmapGridRows ?? 6, 2, 20),
                HeatmapGridColumns = Math.Clamp(raw.Clicks?.HeatmapGridColumns ?? 6, 2, 20)
            },
            GeoIp = new LandingGeoIpRuntimeConfigDto
            {
                Enabled = raw.GeoIp?.Enabled ?? true,
                Provider = string.IsNullOrWhiteSpace(raw.GeoIp?.Provider) ? "ipwhois" : raw.GeoIp.Provider.Trim(),
                BaseUrl = string.IsNullOrWhiteSpace(raw.GeoIp?.BaseUrl) ? "https://ipwho.is" : raw.GeoIp.BaseUrl.Trim().TrimEnd('/'),
                TimeoutMs = Math.Clamp(raw.GeoIp?.TimeoutMs ?? 1200, 500, 10_000),
                CacheMinutes = Math.Clamp(raw.GeoIp?.CacheMinutes ?? 1440, 1, 10_080)
            }
        };
    }
}
