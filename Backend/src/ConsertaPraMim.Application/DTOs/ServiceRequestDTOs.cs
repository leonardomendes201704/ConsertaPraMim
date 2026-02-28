using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public record CreateServiceRequestDto(
    Guid? CategoryId,
    ServiceCategory? Category,
    string Description,
    string Street,
    string City,
    string Zip,
    double Lat,
    double Lng,
    string? ProblemAnalysisSummary = null,
    string? ProblemAnalysisHighlightsJson = null,
    string? Neighborhood = null);
public record ServiceRequestDto(
    Guid Id,
    string Status,
    string Category,
    string Description,
    DateTime CreatedAt,
    string Street,
    string City,
    string Zip,
    string? ClientName = null,
    string? ClientPhone = null,
    string? ImageUrl = null,
    int? Rating = null,
    string? ReviewComment = null,
    decimal? EstimatedValue = null,
    double? DistanceKm = null,
    int CommercialVersion = 0,
    string? CommercialState = null,
    decimal? CommercialBaseValue = null,
    decimal? CommercialCurrentValue = null,
    DateTime? CommercialUpdatedAtUtc = null,
    int? ClientRating = null,
    string? ClientReviewComment = null,
    Guid? ClientUserId = null,
    double? Latitude = null,
    double? Longitude = null,
    string? CategoryIcon = null,
    string? ProblemAnalysisSummary = null,
    IReadOnlyList<string>? ProblemAnalysisHighlights = null,
    bool HasProblemAnalysis = false,
    string? Neighborhood = null);

public record CancelServiceRequestDto(
    string Reason);

public record CancelServiceRequestResultDto(
    bool Success,
    ServiceRequestDto? Request = null,
    IReadOnlyList<Guid>? CancelledAppointmentIds = null,
    int NotifiedProviderCount = 0,
    string? ErrorCode = null,
    string? ErrorMessage = null);
