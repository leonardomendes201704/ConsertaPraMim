using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public record CreateServiceRequestDto(Guid? CategoryId, ServiceCategory? Category, string Description, string Street, string City, string Zip, double Lat, double Lng);
public record ServiceRequestDto(Guid Id, string Status, string Category, string Description, DateTime CreatedAt, string Street, string City, string Zip, string? ClientName = null, string? ClientPhone = null, string? ImageUrl = null, int? Rating = null, string? ReviewComment = null, decimal? EstimatedValue = null, double? DistanceKm = null);
