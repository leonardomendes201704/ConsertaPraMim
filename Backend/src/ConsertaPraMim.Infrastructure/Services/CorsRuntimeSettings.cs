using ConsertaPraMim.Application.Constants;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ConsertaPraMim.Infrastructure.Services;

public class CorsRuntimeSettings : ICorsRuntimeSettings
{
    private const string CorsConfigCacheKey = "cors.runtime.allowed-origins";
    private static readonly TimeSpan CorsConfigCacheTtl = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<CorsRuntimeSettings> _logger;
    private readonly IReadOnlyList<string> _defaultAllowedOrigins;

    public CorsRuntimeSettings(
        IServiceScopeFactory scopeFactory,
        IMemoryCache memoryCache,
        ILogger<CorsRuntimeSettings> logger)
    {
        _scopeFactory = scopeFactory;
        _memoryCache = memoryCache;
        _logger = logger;
        _defaultAllowedOrigins = NormalizeOrigins(
        [
            "https://localhost:7167",
            "http://localhost:5069",
            "https://localhost:7297",
            "http://localhost:5140",
            "https://localhost:7225",
            "http://localhost:5151",
            "http://localhost:5173",
            "http://localhost:5174",
            "capacitor://localhost",
            "ionic://localhost"
        ]);
    }

    public async Task<AdminCorsRuntimeConfigDto> GetCorsConfigAsync(
        CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(CorsConfigCacheKey, out var cachedValue) &&
            cachedValue is CorsConfigCacheEntry cached)
        {
            return cached.Config;
        }

        var loaded = await LoadConfigFromDatabaseAsync(cancellationToken);
        _memoryCache.Set(CorsConfigCacheKey, loaded, CorsConfigCacheTtl);
        return loaded.Config;
    }

    public bool IsOriginAllowed(string? origin)
    {
        var normalizedOrigin = NormalizeOrigin(origin);
        if (normalizedOrigin == null)
        {
            return false;
        }

        var cacheEntry = GetOrLoadCacheEntry();
        return cacheEntry.AllowedOriginsSet.Contains(normalizedOrigin);
    }

    public void InvalidateCorsCache()
    {
        _memoryCache.Remove(CorsConfigCacheKey);
    }

    private CorsConfigCacheEntry GetOrLoadCacheEntry()
    {
        if (_memoryCache.TryGetValue(CorsConfigCacheKey, out var cachedValue) &&
            cachedValue is CorsConfigCacheEntry cached)
        {
            return cached;
        }

        var loaded = LoadConfigFromDatabaseSync();
        _memoryCache.Set(CorsConfigCacheKey, loaded, CorsConfigCacheTtl);
        return loaded;
    }

    private async Task<CorsConfigCacheEntry> LoadConfigFromDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ConsertaPraMimDbContext>();
            var setting = await dbContext.SystemSettings
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Key == SystemSettingKeys.CorsAllowedOrigins,
                    cancellationToken);

            if (setting == null)
            {
                return BuildConfig(_defaultAllowedOrigins, DateTime.UtcNow);
            }

            var parsedOrigins = ParseStoredOrigins(setting.Value);
            return BuildConfig(
                parsedOrigins,
                setting.UpdatedAt ?? setting.CreatedAt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao carregar configuracao de CORS do banco. Usando fallback de appsettings.");
            return BuildConfig(_defaultAllowedOrigins, DateTime.UtcNow);
        }
    }

    private CorsConfigCacheEntry LoadConfigFromDatabaseSync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ConsertaPraMimDbContext>();
            var setting = dbContext.SystemSettings
                .AsNoTracking()
                .SingleOrDefault(x => x.Key == SystemSettingKeys.CorsAllowedOrigins);

            if (setting == null)
            {
                return BuildConfig(_defaultAllowedOrigins, DateTime.UtcNow);
            }

            var parsedOrigins = ParseStoredOrigins(setting.Value);
            return BuildConfig(parsedOrigins, setting.UpdatedAt ?? setting.CreatedAt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao carregar configuracao de CORS (sync). Usando fallback de appsettings.");
            return BuildConfig(_defaultAllowedOrigins, DateTime.UtcNow);
        }
    }

    private static CorsConfigCacheEntry BuildConfig(
        IReadOnlyList<string> origins,
        DateTime updatedAtUtc)
    {
        var list = origins.ToList();
        var set = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
        return new CorsConfigCacheEntry(
            new AdminCorsRuntimeConfigDto(list, updatedAtUtc),
            set);
    }

    private IReadOnlyList<string> ParseStoredOrigins(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var trimmed = raw.Trim();

        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(trimmed);
                return NormalizeOrigins(parsed ?? []);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Valor de CORS no banco nao esta em JSON valido. Tentando fallback por linhas.");
            }
        }

        var splitValues = trimmed
            .Split(['\r', '\n', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return NormalizeOrigins(splitValues);
    }

    private static IReadOnlyList<string> NormalizeOrigins(IEnumerable<string> origins)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var origin in origins)
        {
            var normalizedOrigin = NormalizeOrigin(origin);
            if (normalizedOrigin == null || !seen.Add(normalizedOrigin))
            {
                continue;
            }

            normalized.Add(normalizedOrigin);
        }

        return normalized;
    }

    private static string? NormalizeOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return null;
        }

        var trimmed = origin.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var normalized = uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped).TrimEnd('/');
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized.ToLowerInvariant();
    }

    private sealed record CorsConfigCacheEntry(
        AdminCorsRuntimeConfigDto Config,
        HashSet<string> AllowedOriginsSet);
}
