namespace ConsertaPraMim.Application.Interfaces;

public sealed record DrivingRouteLookupResult(
    bool Success,
    double DistanceMeters,
    double DurationSeconds,
    IReadOnlyList<double[]> Geometry,
    string? ErrorMessage = null,
    bool IsFromCache = false);

public interface IDrivingRouteService
{
    Task<DrivingRouteLookupResult> GetDrivingRouteAsync(
        double providerLatitude,
        double providerLongitude,
        double requestLatitude,
        double requestLongitude,
        CancellationToken cancellationToken = default);
}

