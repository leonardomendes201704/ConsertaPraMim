using System.Globalization;
using ConsertaPraMim.Application.Interfaces;

namespace ConsertaPraMim.Web.Provider.Services;

public class ProviderApiDrivingRouteService : IDrivingRouteService
{
    private readonly ProviderApiCaller _apiCaller;

    public ProviderApiDrivingRouteService(ProviderApiCaller apiCaller)
    {
        _apiCaller = apiCaller;
    }

    public async Task<DrivingRouteLookupResult> GetDrivingRouteAsync(
        double providerLatitude,
        double providerLongitude,
        double requestLatitude,
        double requestLongitude,
        CancellationToken cancellationToken = default)
    {
        var path =
            "/api/routes/driving" +
            $"?providerLat={providerLatitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&providerLng={providerLongitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&requestLat={requestLatitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&requestLng={requestLongitude.ToString(CultureInfo.InvariantCulture)}";

        var response = await _apiCaller.SendAsync<DrivingRouteApiResponse>(HttpMethod.Get, path, cancellationToken: cancellationToken);
        var payload = response.Payload;
        if (!response.Success || payload == null || !payload.Success)
        {
            return new DrivingRouteLookupResult(
                false,
                0,
                0,
                [],
                payload?.Message ?? response.ErrorMessage ?? "Nao foi possivel calcular rota de carro no momento.");
        }

        return new DrivingRouteLookupResult(
            true,
            payload.Distance,
            payload.Duration,
            payload.Geometry ?? [],
            null,
            false);
    }

    private sealed record DrivingRouteApiResponse(
        bool Success,
        double Distance,
        double Duration,
        IReadOnlyList<double[]>? Geometry,
        string? Message);
}
