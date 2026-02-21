using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Web.Admin.Security;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace ConsertaPraMim.Web.Admin.Services;

public sealed class AdminPortalLinksService : IAdminPortalLinksService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);
    private const string CacheKey = "admin:portal-links";

    private readonly IAdminOperationsApiClient _adminOperationsApiClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _hostEnvironment;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<AdminPortalLinksService> _logger;

    public AdminPortalLinksService(
        IAdminOperationsApiClient adminOperationsApiClient,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        IWebHostEnvironment hostEnvironment,
        IMemoryCache memoryCache,
        ILogger<AdminPortalLinksService> logger)
    {
        _adminOperationsApiClient = adminOperationsApiClient;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<AdminPortalLinksDto> GetPortalLinksAsync(CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(CacheKey, out AdminPortalLinksDto? cached) && cached != null)
        {
            return cached;
        }

        var fallback = BuildFallback();
        var accessToken = _httpContextAccessor.HttpContext?.User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _memoryCache.Set(CacheKey, fallback, TimeSpan.FromSeconds(10));
            return fallback;
        }

        try
        {
            var targetProfile = ResolvePortalProfileTarget();
            var response = await _adminOperationsApiClient.GetMonitoringConfigSectionsAsync(
                accessToken,
                cancellationToken);

            if (!response.Success || response.Data == null)
            {
                _memoryCache.Set(CacheKey, fallback, TimeSpan.FromSeconds(10));
                return fallback;
            }

            var resolved = ResolveFromConfigSections(response.Data, targetProfile, fallback);
            _memoryCache.Set(CacheKey, resolved, CacheDuration);
            return resolved;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _memoryCache.Set(CacheKey, fallback, TimeSpan.FromSeconds(5));
            return fallback;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falha ao resolver links de portais via secoes de configuracao runtime.");
            _memoryCache.Set(CacheKey, fallback, TimeSpan.FromSeconds(10));
            return fallback;
        }
    }

    private AdminPortalLinksDto BuildFallback()
    {
        var providerUrl = NormalizeAbsoluteUrl(_configuration["Portals:ProviderUrl"]) ?? "http://localhost:5140/";
        var clientUrl = NormalizeAbsoluteUrl(_configuration["Portals:ClientUrl"]) ?? "http://localhost:5069/";
        return new AdminPortalLinksDto(providerUrl, clientUrl);
    }

    private static AdminPortalLinksDto ResolveFromConfigSections(
        AdminRuntimeConfigSectionsResponseDto sectionsResponse,
        string targetProfile,
        AdminPortalLinksDto fallback)
    {
        var sections = sectionsResponse.Items ?? [];
        if (sections.Count == 0)
        {
            return fallback;
        }

        var adminPortalsSection = sections.FirstOrDefault(x =>
            x.SectionPath.Equals("AdminPortals", StringComparison.OrdinalIgnoreCase));

        if (adminPortalsSection != null && !string.IsNullOrWhiteSpace(adminPortalsSection.JsonValue))
        {
            var linksFromAdminPortals = TryParseAdminPortalsSection(
                adminPortalsSection.JsonValue,
                targetProfile,
                fallback);
            if (!string.Equals(linksFromAdminPortals.ProviderUrl, fallback.ProviderUrl, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(linksFromAdminPortals.ClientUrl, fallback.ClientUrl, StringComparison.OrdinalIgnoreCase))
            {
                return linksFromAdminPortals;
            }
        }

        var monitoringSection = sections.FirstOrDefault(x =>
            x.SectionPath.Equals("Monitoring", StringComparison.OrdinalIgnoreCase));

        if (monitoringSection == null || string.IsNullOrWhiteSpace(monitoringSection.JsonValue))
        {
            return fallback;
        }

        return TryParseMonitoringSection(monitoringSection.JsonValue, fallback);
    }

    private static AdminPortalLinksDto TryParseAdminPortalsSection(
        string json,
        string targetProfile,
        AdminPortalLinksDto fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return fallback;
            }

            var rootResolved = new AdminPortalLinksDto(
                ProviderUrl: NormalizeAbsoluteUrl(TryReadPropertyString(document.RootElement, "ProviderUrl")) ?? fallback.ProviderUrl,
                ClientUrl: NormalizeAbsoluteUrl(TryReadPropertyString(document.RootElement, "ClientUrl")) ?? fallback.ClientUrl);

            var targetNode = TryGetTargetNode(document.RootElement, targetProfile);
            if (targetNode.HasValue && targetNode.Value.ValueKind == JsonValueKind.Object)
            {
                return new AdminPortalLinksDto(
                    ProviderUrl: NormalizeAbsoluteUrl(TryReadPropertyString(targetNode.Value, "ProviderUrl")) ?? rootResolved.ProviderUrl,
                    ClientUrl: NormalizeAbsoluteUrl(TryReadPropertyString(targetNode.Value, "ClientUrl")) ?? rootResolved.ClientUrl);
            }

            return rootResolved;
        }
        catch
        {
            return fallback;
        }
    }

    private static AdminPortalLinksDto TryParseMonitoringSection(
        string json,
        AdminPortalLinksDto fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return fallback;
            }

            var dependencyHealth = TryReadProperty(document.RootElement, "DependencyHealth");
            if (dependencyHealth is null || dependencyHealth.Value.ValueKind != JsonValueKind.Object)
            {
                return fallback;
            }

            var providerUrl = NormalizeAbsoluteUrl(TryReadPropertyString(dependencyHealth.Value, "ProviderPortalUrl")) ?? fallback.ProviderUrl;
            var clientUrl = NormalizeAbsoluteUrl(TryReadPropertyString(dependencyHealth.Value, "ClientPortalUrl")) ?? fallback.ClientUrl;
            return new AdminPortalLinksDto(providerUrl, clientUrl);
        }
        catch
        {
            return fallback;
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
        if (element.ValueKind != JsonValueKind.Object)
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

    private string ResolvePortalProfileTarget()
    {
        var configuredTarget = _configuration["AdminPortals:Target"];
        return ResolvePortalProfileTargetCore(configuredTarget, _hostEnvironment.EnvironmentName);
    }

    private static string ResolvePortalProfileTargetCore(string? configuredTarget, string? environmentName)
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
}
