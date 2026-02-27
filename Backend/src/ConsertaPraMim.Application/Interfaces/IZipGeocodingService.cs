namespace ConsertaPraMim.Application.Interfaces;

public interface IZipGeocodingService
{
    Task<(string NormalizedZip, double Latitude, double Longitude, string? Street, string? Neighborhood, string? City)?> ResolveCoordinatesAsync(
        string? zipCode,
        string? street = null,
        string? city = null);

    Task<(string NormalizedZip, string? Street, string? Neighborhood, string? City)?> ResolveAddressByCoordinatesAsync(
        double latitude,
        double longitude);
}
