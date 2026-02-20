using ConsertaPraMim.Application.Constants;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConsertaPraMim.Infrastructure.Services;

public class MonitoringRuntimeSettings : IMonitoringRuntimeSettings
{
    private const string TelemetryEnabledCacheKey = "monitoring.runtime.telemetry.enabled";
    private static readonly TimeSpan TelemetryEnabledCacheTtl = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly bool _defaultTelemetryEnabled;

    public MonitoringRuntimeSettings(
        IServiceScopeFactory scopeFactory,
        IMemoryCache memoryCache,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _memoryCache = memoryCache;
        _defaultTelemetryEnabled = ParseBool(configuration["Monitoring:Enabled"], defaultValue: true);
    }

    public async Task<AdminMonitoringRuntimeConfigDto> GetTelemetryConfigAsync(
        CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(TelemetryEnabledCacheKey, out var cachedValue) &&
            cachedValue is AdminMonitoringRuntimeConfigDto cached)
        {
            return cached;
        }

        var config = await LoadTelemetryConfigFromDatabaseAsync(cancellationToken);
        _memoryCache.Set(TelemetryEnabledCacheKey, config, TelemetryEnabledCacheTtl);
        return config;
    }

    public async Task<bool> IsTelemetryEnabledAsync(CancellationToken cancellationToken = default)
    {
        var config = await GetTelemetryConfigAsync(cancellationToken);
        return config.TelemetryEnabled;
    }

    public void InvalidateTelemetryCache()
    {
        _memoryCache.Remove(TelemetryEnabledCacheKey);
    }

    private async Task<AdminMonitoringRuntimeConfigDto> LoadTelemetryConfigFromDatabaseAsync(
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ConsertaPraMimDbContext>();

        var setting = await dbContext.SystemSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Key == SystemSettingKeys.MonitoringTelemetryEnabled,
                cancellationToken);

        if (setting == null)
        {
            return new AdminMonitoringRuntimeConfigDto(
                TelemetryEnabled: _defaultTelemetryEnabled,
                UpdatedAtUtc: DateTime.UtcNow);
        }

        return new AdminMonitoringRuntimeConfigDto(
            TelemetryEnabled: ParseBool(setting.Value, _defaultTelemetryEnabled),
            UpdatedAtUtc: setting.UpdatedAt ?? setting.CreatedAt);
    }

    private static bool ParseBool(string? raw, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        var normalized = raw.Trim();
        if (bool.TryParse(normalized, out var parsed))
        {
            return parsed;
        }

        return normalized switch
        {
            "1" => true,
            "0" => false,
            _ => defaultValue
        };
    }
}
