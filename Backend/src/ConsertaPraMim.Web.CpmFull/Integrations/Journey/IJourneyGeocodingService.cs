namespace AppMobileCPM.Integrations.Journey;

public interface IJourneyGeocodingService
{
    Task<JourneyGeocodingResult?> ResolveAsync(
        string? postalCode,
        string? street = null,
        string? city = null,
        CancellationToken cancellationToken = default);
}
