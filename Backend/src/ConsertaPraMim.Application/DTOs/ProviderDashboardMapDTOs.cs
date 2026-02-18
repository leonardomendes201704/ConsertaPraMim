namespace ConsertaPraMim.Application.DTOs;

public record ProviderServiceMapPinDto(
    Guid RequestId,
    string Category,
    string? CategoryIcon,
    string Description,
    string Street,
    string City,
    string Zip,
    DateTime CreatedAt,
    double Latitude,
    double Longitude,
    double DistanceKm,
    bool IsWithinInterestRadius,
    bool IsCategoryMatch);
