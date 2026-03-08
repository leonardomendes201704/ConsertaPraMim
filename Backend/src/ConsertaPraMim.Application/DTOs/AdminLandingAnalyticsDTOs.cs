using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public sealed record AdminLandingAnalyticsQueryDto(
    string? SearchTerm,
    string? Origin,
    string? Path,
    string? CountryCode,
    string? Region,
    string? City,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page,
    int PageSize);

public sealed record AdminLandingAnalyticsBreakdownItemDto(
    string Label,
    int Count);

public sealed record AdminLandingAnalyticsHeatmapCellDto(
    int Row,
    int Column,
    int Hits);

public sealed record AdminLandingAnalyticsSessionListItemDto(
    string SessionId,
    string VisitorId,
    DateTime StartedAtUtc,
    DateTime LastActivityAtUtc,
    string Path,
    LandingLeadOrigin? InitialLeadOrigin,
    string EstimatedLocality,
    int ActiveSeconds,
    int MaxScrollPercent,
    int Clicks,
    int ModalOpens,
    int LeadSubmissions,
    Guid? LeadId);

public sealed record AdminLandingAnalyticsOverviewDto(
    DateTime FromUtc,
    DateTime ToUtc,
    int TotalSessions,
    int TotalUniqueVisitors,
    int SessionsWithGeo,
    int TotalHeartbeats,
    int TotalActiveSeconds,
    double AverageActiveSecondsPerSession,
    double AverageMaxScrollPercent,
    int SessionsWithClicks,
    int TotalClicks,
    int LeadModalOpens,
    int LeadSubmissions,
    double LeadSubmissionRatePercent,
    int HeatmapRows,
    int HeatmapColumns,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<AdminLandingAnalyticsBreakdownItemDto> PathBreakdown,
    IReadOnlyList<AdminLandingAnalyticsBreakdownItemDto> OriginBreakdown,
    IReadOnlyList<AdminLandingAnalyticsBreakdownItemDto> CountryBreakdown,
    IReadOnlyList<AdminLandingAnalyticsBreakdownItemDto> RegionBreakdown,
    IReadOnlyList<AdminLandingAnalyticsBreakdownItemDto> CityBreakdown,
    IReadOnlyList<AdminLandingAnalyticsBreakdownItemDto> EventBreakdown,
    IReadOnlyList<AdminLandingAnalyticsHeatmapCellDto> Heatmap,
    IReadOnlyList<AdminLandingAnalyticsSessionListItemDto> Sessions);

public sealed record AdminLandingAnalyticsSessionGeoDto(
    string? Country,
    string? CountryCode,
    string? Region,
    string? RegionCode,
    string? City,
    string? Provider,
    string? LookupStatus);

public sealed record AdminLandingAnalyticsSessionLeadDto(
    Guid Id,
    LandingLeadOrigin Origin,
    string FullName,
    string Email,
    string Phone,
    string Locality,
    DateTime CreatedAtUtc);

public sealed record AdminLandingAnalyticsTimelineItemDto(
    string Source,
    string Type,
    DateTime OccurredAtUtc,
    string Description,
    string? MetadataJson);

public sealed record AdminLandingAnalyticsSessionDetailsDto(
    string SessionId,
    string VisitorId,
    DateTime StartedAtUtc,
    DateTime LastActivityAtUtc,
    string Path,
    string? CurrentUrl,
    LandingLeadOrigin? InitialLeadOrigin,
    int ActiveSeconds,
    int MaxScrollPercent,
    int Clicks,
    int ModalOpens,
    int LeadSubmissions,
    string EstimatedLocality,
    string? IpAddress,
    string? ForwardedFor,
    string? UserAgent,
    string? AcceptLanguage,
    string? RefererUrl,
    AdminLandingAnalyticsSessionGeoDto Geo,
    AdminLandingAnalyticsSessionLeadDto? Lead,
    IReadOnlyList<AdminLandingAnalyticsTimelineItemDto> Timeline);
