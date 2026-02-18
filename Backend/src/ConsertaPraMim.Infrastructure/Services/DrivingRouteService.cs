using System.Globalization;
using System.Text.Json;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ConsertaPraMim.Infrastructure.Services;

public sealed class DrivingRouteService : IDrivingRouteService
{
    private static readonly string[] DrivingRouteProviderTemplates =
    {
        "https://router.project-osrm.org/route/v1/driving/{0}",
        "https://routing.openstreetmap.de/routed-car/route/v1/driving/{0}"
    };

    private static readonly TimeSpan SuccessCacheSlidingExpiration = TimeSpan.FromHours(6);
    private static readonly TimeSpan SuccessCacheAbsoluteExpiration = TimeSpan.FromHours(24);
    private static readonly TimeSpan FailureCacheDuration = TimeSpan.FromMinutes(1);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<DrivingRouteService> _logger;

    public DrivingRouteService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache,
        ILogger<DrivingRouteService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<DrivingRouteLookupResult> GetDrivingRouteAsync(
        double providerLatitude,
        double providerLongitude,
        double requestLatitude,
        double requestLongitude,
        CancellationToken cancellationToken = default)
    {
        if (!AreValidCoordinates(providerLatitude, providerLongitude) ||
            !AreValidCoordinates(requestLatitude, requestLongitude))
        {
            return new DrivingRouteLookupResult(
                false,
                0d,
                0d,
                Array.Empty<double[]>(),
                "Coordenadas invalidas para calculo de rota.");
        }

        var cacheKey = BuildCacheKey(providerLatitude, providerLongitude, requestLatitude, requestLongitude);
        if (_memoryCache.TryGetValue<DrivingRouteLookupResult>(cacheKey, out var cachedResult))
        {
            return cachedResult with { IsFromCache = true };
        }

        var routePath =
            $"{FormatCoordinate(providerLongitude)},{FormatCoordinate(providerLatitude)};" +
            $"{FormatCoordinate(requestLongitude)},{FormatCoordinate(requestLatitude)}" +
            "?alternatives=false&steps=false&overview=full&geometries=geojson";

        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(8);

        foreach (var template in DrivingRouteProviderTemplates)
        {
            var routeUrl = string.Format(CultureInfo.InvariantCulture, template, routePath);

            try
            {
                using var response = await client.GetAsync(routeUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = json.RootElement;

                if (!root.TryGetProperty("code", out var codeElement) ||
                    !string.Equals(codeElement.GetString(), "Ok", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!root.TryGetProperty("routes", out var routesElement) ||
                    routesElement.ValueKind != JsonValueKind.Array ||
                    routesElement.GetArrayLength() == 0)
                {
                    continue;
                }

                var routeElement = routesElement[0];
                if (!routeElement.TryGetProperty("geometry", out var geometryElement) ||
                    !geometryElement.TryGetProperty("coordinates", out var coordinatesElement) ||
                    coordinatesElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var latLngPoints = new List<double[]>();
                foreach (var coordinate in coordinatesElement.EnumerateArray())
                {
                    if (coordinate.ValueKind != JsonValueKind.Array || coordinate.GetArrayLength() < 2)
                    {
                        continue;
                    }

                    var longitude = coordinate[0].GetDouble();
                    var latitude = coordinate[1].GetDouble();
                    latLngPoints.Add(new[] { latitude, longitude });
                }

                if (latLngPoints.Count < 2)
                {
                    continue;
                }

                var distance = routeElement.TryGetProperty("distance", out var distanceElement)
                    ? distanceElement.GetDouble()
                    : 0d;
                var duration = routeElement.TryGetProperty("duration", out var durationElement)
                    ? durationElement.GetDouble()
                    : 0d;

                var successResult = new DrivingRouteLookupResult(
                    true,
                    distance,
                    duration,
                    latLngPoints);

                var cacheOptions = new MemoryCacheEntryOptions
                {
                    SlidingExpiration = SuccessCacheSlidingExpiration,
                    AbsoluteExpirationRelativeToNow = SuccessCacheAbsoluteExpiration
                };
                _memoryCache.Set(cacheKey, successResult, cacheOptions);

                return successResult;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    ex,
                    "Falha ao consultar provedor de rota {RouteProvider}. Tentando proximo provedor.",
                    routeUrl);
            }
        }

        var failureResult = new DrivingRouteLookupResult(
            false,
            0d,
            0d,
            Array.Empty<double[]>(),
            "Nao foi possivel calcular rota de carro no momento.");

        _memoryCache.Set(cacheKey, failureResult, FailureCacheDuration);
        return failureResult;
    }

    private static string BuildCacheKey(
        double providerLatitude,
        double providerLongitude,
        double requestLatitude,
        double requestLongitude)
        => $"driving-route:v1:{FormatCoordinate(providerLatitude)}:{FormatCoordinate(providerLongitude)}:{FormatCoordinate(requestLatitude)}:{FormatCoordinate(requestLongitude)}";

    private static bool AreValidCoordinates(double latitude, double longitude)
        => latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    private static string FormatCoordinate(double value)
        => value.ToString("0.######", CultureInfo.InvariantCulture);
}

