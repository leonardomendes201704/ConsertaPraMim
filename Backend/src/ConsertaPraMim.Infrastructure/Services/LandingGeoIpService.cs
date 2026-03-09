using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ConsertaPraMim.Infrastructure.Services;

public sealed class LandingGeoIpService : ILandingGeoIpService
{
    private const string ProviderName = "ipwhois";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ILandingAnalyticsRuntimeSettings _runtimeSettings;
    private readonly ILogger<LandingGeoIpService> _logger;

    public LandingGeoIpService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache,
        ILandingAnalyticsRuntimeSettings runtimeSettings,
        ILogger<LandingGeoIpService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _memoryCache = memoryCache;
        _runtimeSettings = runtimeSettings;
        _logger = logger;
    }

    public async Task<LandingGeoIpLookupResultDto> LookupAsync(
        string? ipAddress,
        string? forwardedFor,
        CancellationToken cancellationToken = default)
    {
        var config = await _runtimeSettings.GetConfigAsync(cancellationToken);
        var provider = string.IsNullOrWhiteSpace(config.GeoIp.Provider) ? ProviderName : config.GeoIp.Provider.Trim();
        var candidateIp = ResolveCandidateIp(ipAddress, forwardedFor);

        if (!config.GeoIp.Enabled)
        {
            return new LandingGeoIpLookupResultDto("disabled", provider, candidateIp, null, null, null, null, null);
        }

        if (string.IsNullOrWhiteSpace(candidateIp))
        {
            return new LandingGeoIpLookupResultDto("ip_not_resolved", provider, null, null, null, null, null, null);
        }

        if (_memoryCache.TryGetValue(BuildCacheKey(candidateIp), out LandingGeoIpLookupResultDto? cached) && cached != null)
        {
            return cached;
        }

        try
        {
            var client = _httpClientFactory.CreateClient(typeof(LandingGeoIpService).FullName ?? nameof(LandingGeoIpService));
            client.Timeout = TimeSpan.FromMilliseconds(config.GeoIp.TimeoutMs);

            var endpointUrl = $"{config.GeoIp.BaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(candidateIp)}";
            var response = await client.GetAsync(endpointUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var failed = new LandingGeoIpLookupResultDto(
                    "http_error",
                    provider,
                    candidateIp,
                    null,
                    null,
                    null,
                    null,
                    null);
                Cache(candidateIp, failed, config.GeoIp.CacheMinutes);
                return failed;
            }

            var payload = await response.Content.ReadFromJsonAsync<IpWhoIsResponse>(cancellationToken);
            if (payload == null || !payload.Success)
            {
                var providerError = new LandingGeoIpLookupResultDto(
                    "provider_error",
                    provider,
                    candidateIp,
                    null,
                    null,
                    null,
                    null,
                    null);
                Cache(candidateIp, providerError, config.GeoIp.CacheMinutes);
                return providerError;
            }

            var result = new LandingGeoIpLookupResultDto(
                "resolved",
                provider,
                payload.Ip ?? candidateIp,
                Normalize(payload.Country, 120),
                Normalize(payload.CountryCode, 8)?.ToUpperInvariant(),
                Normalize(payload.Region, 120),
                Normalize(payload.RegionCode, 32)?.ToUpperInvariant(),
                Normalize(payload.City, 120));

            Cache(candidateIp, result, config.GeoIp.CacheMinutes);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao consultar GeoIP para o IP {IpAddress}.", candidateIp);
            var failed = new LandingGeoIpLookupResultDto("lookup_failed", provider, candidateIp, null, null, null, null, null);
            Cache(candidateIp, failed, config.GeoIp.CacheMinutes);
            return failed;
        }
    }

    private void Cache(string candidateIp, LandingGeoIpLookupResultDto result, int cacheMinutes)
    {
        _memoryCache.Set(BuildCacheKey(candidateIp), result, TimeSpan.FromMinutes(cacheMinutes));
    }

    private static string BuildCacheKey(string ipAddress)
        => $"landing:geoip:{ipAddress.Trim()}";

    private static string? ResolveCandidateIp(string? ipAddress, string? forwardedFor)
    {
        foreach (var candidate in EnumerateCandidates(forwardedFor, ipAddress))
        {
            if (!IPAddress.TryParse(candidate, out var parsedIp))
            {
                continue;
            }

            if (IsPublicIpAddress(parsedIp))
            {
                return parsedIp.ToString();
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCandidates(string? forwardedFor, string? ipAddress)
    {
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            foreach (var raw in forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    yield return raw.Trim();
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            yield return ipAddress.Trim();
        }
    }

    private static bool IsPublicIpAddress(IPAddress ipAddress)
    {
        if (IPAddress.IsLoopback(ipAddress))
        {
            return false;
        }

        if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = ipAddress.GetAddressBytes();
            if (bytes[0] == 10 ||
                bytes[0] == 127 ||
                (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168) ||
                (bytes[0] == 169 && bytes[1] == 254))
            {
                return false;
            }

            return true;
        }

        if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return !ipAddress.IsIPv6LinkLocal &&
                   !ipAddress.IsIPv6Multicast &&
                   !ipAddress.IsIPv6SiteLocal &&
                   !ipAddress.IsIPv6Teredo;
        }

        return false;
    }

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private sealed class IpWhoIsResponse
    {
        public bool Success { get; set; }
        public string? Ip { get; set; }
        public string? Country { get; set; }
        [JsonPropertyName("country_code")]
        public string? CountryCode { get; set; }
        public string? Region { get; set; }
        [JsonPropertyName("region_code")]
        public string? RegionCode { get; set; }
        public string? City { get; set; }
    }
}
