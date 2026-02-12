using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public record CreateServiceRequestDto(ServiceCategory Category, string Description, string Street, string City, string Zip, double Lat, double Lng);
public record ServiceRequestDto(Guid Id, string Status, string Category, string Description, DateTime CreatedAt, string AddressCity);
