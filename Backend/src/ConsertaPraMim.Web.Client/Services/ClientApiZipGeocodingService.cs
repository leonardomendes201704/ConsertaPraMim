using ConsertaPraMim.Application.Interfaces;

namespace ConsertaPraMim.Web.Client.Services;

public class ClientApiZipGeocodingService : IZipGeocodingService
{
    private readonly ClientApiCaller _apiCaller;

    public ClientApiZipGeocodingService(ClientApiCaller apiCaller)
    {
        _apiCaller = apiCaller;
    }

    public async Task<(string NormalizedZip, double Latitude, double Longitude, string? Street, string? City)?> ResolveCoordinatesAsync(
        string? zipCode,
        string? street = null,
        string? city = null)
    {
        if (string.IsNullOrWhiteSpace(zipCode))
        {
            return null;
        }

        var path = $"/api/service-requests/zip-resolution?zipCode={Uri.EscapeDataString(zipCode.Trim())}";
        var response = await _apiCaller.SendAsync<ZipResolutionResponse>(HttpMethod.Get, path);
        return response.Payload == null
            ? null
            : (response.Payload.ZipCode, response.Payload.Latitude, response.Payload.Longitude, response.Payload.Street, response.Payload.City);
    }

    private sealed record ZipResolutionResponse(
        string ZipCode,
        double Latitude,
        double Longitude,
        string? Street,
        string? City);
}
