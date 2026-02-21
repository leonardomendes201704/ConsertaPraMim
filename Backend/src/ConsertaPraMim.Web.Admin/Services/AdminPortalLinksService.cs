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
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<AdminPortalLinksService> _logger;

    public AdminPortalLinksService(
        IAdminOperationsApiClient adminOperationsApiClient,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        IMemoryCache memoryCache,
        ILogger<AdminPortalLinksService> logger)
    {
        _adminOperationsApiClient = adminOperationsApiClient;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
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
            var response = await _adminOperationsApiClient.GetMonitoringConfigSectionsAsync(
                accessToken,
                cancellationToken);

            if (!response.Success || response.Data == null)
            {
                _memoryCache.Set(CacheKey, fallback, TimeSpan.FromSeconds(10));
                return fallback;
            }

            var resolved = ResolveFromConfigSections(response.Data, fallback);
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
            var linksFromAdminPortals = TryParseAdminPortalsSection(adminPortalsSection.JsonValue, fallback);
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
        AdminPortalLinksDto fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return fallback;
            }

            var providerUrl = NormalizeAbsoluteUrl(TryReadPropertyString(document.RootElement, "ProviderUrl")) ?? fallback.ProviderUrl;
            var clientUrl = NormalizeAbsoluteUrl(TryReadPropertyString(document.RootElement, "ClientUrl")) ?? fallback.ClientUrl;
            return new AdminPortalLinksDto(providerUrl, clientUrl);
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
}
