using ConsertaPraMim.Application.Constants;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace ConsertaPraMim.Infrastructure.Services;

public class CorsRuntimeSettings : ICorsRuntimeSettings
{
    private const string CorsConfigCacheKey = "cors.runtime.allowed-origins";
    private static readonly TimeSpan CorsConfigCacheTtl = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<CorsRuntimeSettings> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _environmentName;
    private readonly IReadOnlyList<string> _fallbackAllowedOrigins;

    public CorsRuntimeSettings(
        IServiceScopeFactory scopeFactory,
        IMemoryCache memoryCache,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<CorsRuntimeSettings> logger)
    {
        _scopeFactory = scopeFactory;
        _memoryCache = memoryCache;
        _configuration = configuration;
        _environmentName = hostEnvironment.EnvironmentName;
        _logger = logger;
        _fallbackAllowedOrigins = BuildFallbackAllowedOrigins(configuration, _environmentName);
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
            var settings = await dbContext.SystemSettings
                .AsNoTracking()
                .Where(x =>
                    x.Key == SystemSettingKeys.CorsAllowedOrigins ||
                    x.Key == SystemSettingKeys.ConfigAdminPortals)
                .ToListAsync(cancellationToken);

            var corsSetting = settings.SingleOrDefault(x => x.Key == SystemSettingKeys.CorsAllowedOrigins);
            var adminPortalsSetting = settings.SingleOrDefault(x => x.Key == SystemSettingKeys.ConfigAdminPortals);
            var mergedOrigins = MergeAllowedOrigins(corsSetting?.Value, adminPortalsSetting?.Value);
            return BuildConfig(
                mergedOrigins,
                ResolveUpdatedAtUtc(corsSetting, adminPortalsSetting));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao carregar configuracao de CORS do banco. Usando fallback de appsettings.");
            return BuildConfig(_fallbackAllowedOrigins, DateTime.UtcNow);
        }
    }

    private CorsConfigCacheEntry LoadConfigFromDatabaseSync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ConsertaPraMimDbContext>();
            var settings = dbContext.SystemSettings
                .AsNoTracking()
                .Where(x =>
                    x.Key == SystemSettingKeys.CorsAllowedOrigins ||
                    x.Key == SystemSettingKeys.ConfigAdminPortals)
                .ToList();

            var corsSetting = settings.SingleOrDefault(x => x.Key == SystemSettingKeys.CorsAllowedOrigins);
            var adminPortalsSetting = settings.SingleOrDefault(x => x.Key == SystemSettingKeys.ConfigAdminPortals);
            var mergedOrigins = MergeAllowedOrigins(corsSetting?.Value, adminPortalsSetting?.Value);
            return BuildConfig(
                mergedOrigins,
                ResolveUpdatedAtUtc(corsSetting, adminPortalsSetting));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao carregar configuracao de CORS (sync). Usando fallback de appsettings.");
            return BuildConfig(_fallbackAllowedOrigins, DateTime.UtcNow);
        }
    }

    private IReadOnlyList<string> MergeAllowedOrigins(string? rawCorsOrigins, string? rawAdminPortals)
    {
        var storedCorsOrigins = ParseStoredOrigins(rawCorsOrigins);
        var targetProfile = ResolvePortalProfileTarget(_configuration["AdminPortals:Target"], _environmentName);
        var adminPortalsOrigins = ParseAdminPortalsOrigins(rawAdminPortals, targetProfile);

        return NormalizeOrigins(
            storedCorsOrigins
                .Concat(adminPortalsOrigins)
                .Concat(_fallbackAllowedOrigins));
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

    private static DateTime ResolveUpdatedAtUtc(SystemSetting? corsSetting, SystemSetting? adminPortalsSetting)
    {
        var updatedCandidates = new List<DateTime>();

        if (corsSetting != null)
        {
            updatedCandidates.Add(corsSetting.UpdatedAt ?? corsSetting.CreatedAt);
        }

        if (adminPortalsSetting != null)
        {
            updatedCandidates.Add(adminPortalsSetting.UpdatedAt ?? adminPortalsSetting.CreatedAt);
        }

        return updatedCandidates.Count == 0
            ? DateTime.UtcNow
            : updatedCandidates.Max();
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

    private static IReadOnlyList<string> ParseAdminPortalsOrigins(string? rawJson, string targetProfile)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var origins = new List<string>();
            AddOriginFromUrl(origins, TryReadPropertyString(document.RootElement, "ClientUrl"));
            AddOriginFromUrl(origins, TryReadPropertyString(document.RootElement, "ProviderUrl"));

            var targetNode = TryGetTargetNode(document.RootElement, targetProfile);
            if (targetNode.HasValue && targetNode.Value.ValueKind == JsonValueKind.Object)
            {
                AddOriginFromUrl(origins, TryReadPropertyString(targetNode.Value, "ClientUrl"));
                AddOriginFromUrl(origins, TryReadPropertyString(targetNode.Value, "ProviderUrl"));
            }

            return NormalizeOrigins(origins);
        }
        catch
        {
            return [];
        }
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

    private static IReadOnlyList<string> BuildFallbackAllowedOrigins(IConfiguration configuration, string? environmentName)
    {
        var origins = new List<string>
        {
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
        };

        AddOriginFromUrl(origins, configuration["Portals:ClientUrl"]);
        AddOriginFromUrl(origins, configuration["Portals:ProviderUrl"]);
        AddOriginFromUrl(origins, configuration["Portals:AdminUrl"]);
        AddOriginFromUrl(origins, configuration["AdminPortals:ClientUrl"]);
        AddOriginFromUrl(origins, configuration["AdminPortals:ProviderUrl"]);

        var targetProfile = ResolvePortalProfileTarget(configuration["AdminPortals:Target"], environmentName);
        foreach (var candidate in BuildTargetCandidates(targetProfile))
        {
            AddOriginFromUrl(origins, configuration[$"AdminPortals:Environments:{candidate}:ClientUrl"]);
            AddOriginFromUrl(origins, configuration[$"AdminPortals:Environments:{candidate}:ProviderUrl"]);
        }

        return NormalizeOrigins(origins);
    }

    private static void AddOriginFromUrl(ICollection<string> origins, string? url)
    {
        var normalizedOrigin = NormalizeOrigin(url);
        if (!string.IsNullOrWhiteSpace(normalizedOrigin))
        {
            origins.Add(normalizedOrigin);
        }
    }

    private static string? TryReadPropertyString(JsonElement element, string propertyName)
    {
        var property = TryReadProperty(element, propertyName);
        if (property is null)
        {
            return null;
        }

        return property.Value.ValueKind == JsonValueKind.String
            ? property.Value.GetString()
            : property.Value.GetRawText();
    }

    private static JsonElement? TryReadProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(propertyName))
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static string ResolvePortalProfileTarget(string? configuredTarget, string? environmentName)
    {
        if (!string.IsNullOrWhiteSpace(configuredTarget))
        {
            var normalizedTarget = configuredTarget.Trim();
            if (!normalizedTarget.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedTarget;
            }
        }

        var runningInContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
        if (string.Equals(runningInContainer, "true", StringComparison.OrdinalIgnoreCase))
        {
            return "Vps";
        }

        if (!string.IsNullOrWhiteSpace(environmentName) &&
            environmentName.Trim().Equals("Production", StringComparison.OrdinalIgnoreCase))
        {
            return "Production";
        }

        return "Local";
    }

    private static JsonElement? TryGetTargetNode(JsonElement root, string targetProfile)
    {
        var candidates = BuildTargetCandidates(targetProfile);

        foreach (var candidate in candidates)
        {
            if (TryGetNamedObjectProperty(root, "Environments", candidate, out var environmentNode))
            {
                return environmentNode;
            }
        }

        foreach (var candidate in candidates)
        {
            if (TryGetNamedObjectProperty(root, candidate, out var topLevelNode))
            {
                return topLevelNode;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> BuildTargetCandidates(string targetProfile)
    {
        var candidates = new List<string>();

        void AddCandidate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var normalized = value.Trim();
            if (!candidates.Any(x => x.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            {
                candidates.Add(normalized);
            }
        }

        AddCandidate(targetProfile);

        var normalized = (targetProfile ?? string.Empty).Trim();
        if (normalized.Equals("Local", StringComparison.OrdinalIgnoreCase))
        {
            AddCandidate("Development");
        }
        else if (normalized.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            AddCandidate("Local");
        }
        else if (normalized.Equals("Vps", StringComparison.OrdinalIgnoreCase))
        {
            AddCandidate("Production");
        }
        else if (normalized.Equals("Production", StringComparison.OrdinalIgnoreCase))
        {
            AddCandidate("Vps");
        }

        return candidates;
    }

    private static bool TryGetNamedObjectProperty(
        JsonElement root,
        string propertyName,
        out JsonElement value)
    {
        value = default;
        if (root.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (!property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase) ||
                property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            value = property.Value;
            return true;
        }

        return false;
    }

    private static bool TryGetNamedObjectProperty(
        JsonElement root,
        string containerPropertyName,
        string targetPropertyName,
        out JsonElement value)
    {
        value = default;
        if (!TryGetNamedObjectProperty(root, containerPropertyName, out var container))
        {
            return false;
        }

        return TryGetNamedObjectProperty(container, targetPropertyName, out value);
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
