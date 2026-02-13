namespace ConsertaPraMim.Application.Interfaces;

public interface IZipGeocodingService
{
    Task<(string NormalizedZip, double Latitude, double Longitude, string? Street, string? City)?> ResolveCoordinatesAsync(
        string? zipCode,
        string? street = null,
        string? city = null);
}
