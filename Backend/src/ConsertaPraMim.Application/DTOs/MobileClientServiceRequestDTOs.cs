namespace ConsertaPraMim.Application.DTOs;

public record MobileClientServiceRequestCategoryDto(
    Guid Id,
    string Name,
    string Slug,
    string LegacyCategory,
    string Icon);

public record MobileClientResolveZipResponseDto(
    string ZipCode,
    string Street,
    string City,
    double Latitude,
    double Longitude);

public record MobileClientCreateServiceRequestRequestDto(
    Guid CategoryId,
    string Description,
    string ZipCode,
    string? Street,
    string? City);

public record MobileClientCreateServiceRequestResponseDto(
    MobileClientOrderItemDto Order,
    string Street,
    string City,
    string ZipCode,
    string Message);
