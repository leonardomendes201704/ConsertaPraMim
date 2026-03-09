using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public sealed class LandingAnalyticsRuntimeConfigDto
{
    public bool ClientTelemetryEnabled { get; init; } = true;
    public LandingHeartbeatRuntimeConfigDto Heartbeat { get; init; } = new();
    public LandingScrollRuntimeConfigDto Scroll { get; init; } = new();
    public LandingClicksRuntimeConfigDto Clicks { get; init; } = new();
    public LandingGeoIpRuntimeConfigDto GeoIp { get; init; } = new();
}

public sealed class LandingHeartbeatRuntimeConfigDto
{
    public bool Enabled { get; init; } = true;
    public int IntervalSeconds { get; init; } = 15;
    public int MaxSessionDurationMinutes { get; init; } = 30;
}

public sealed class LandingScrollRuntimeConfigDto
{
    public bool Enabled { get; init; } = true;
    public IReadOnlyList<int> MilestonesPercent { get; init; } = [25, 50, 75, 100];
}

public sealed class LandingClicksRuntimeConfigDto
{
    public bool Enabled { get; init; } = true;
    public bool TrackInteractiveOnly { get; init; } = true;
    public int HeatmapGridRows { get; init; } = 6;
    public int HeatmapGridColumns { get; init; } = 6;
}

public sealed class LandingGeoIpRuntimeConfigDto
{
    public bool Enabled { get; init; } = true;
    public string Provider { get; init; } = "ipwhois";
    public string BaseUrl { get; init; } = "https://ipwho.is";
    public int TimeoutMs { get; init; } = 1200;
    public int CacheMinutes { get; init; } = 1440;
}

public sealed class LandingAnalyticsPublicConfigDto
{
    public bool Enabled { get; init; }
    public LandingHeartbeatRuntimeConfigDto Heartbeat { get; init; } = new();
    public LandingScrollRuntimeConfigDto Scroll { get; init; } = new();
    public LandingClicksRuntimeConfigDto Clicks { get; init; } = new();
}

public sealed record RecordLandingTelemetryBatchRequestDto(
    string? VisitorId,
    string? SessionId,
    string? CurrentUrl,
    string? Path,
    string? Host,
    string? Scheme,
    string? InitialLeadOrigin,
    int? ViewportWidth,
    int? ViewportHeight,
    string? BrowserLanguage,
    IReadOnlyList<RecordLandingTelemetryEventItemDto>? Events);

public sealed record RecordLandingTelemetryEventItemDto(
    string? Type,
    DateTime? OccurredAtUtc,
    int? ActiveSeconds,
    int? ScrollDepthPercent,
    double? ClickXPercent,
    double? ClickYPercent,
    int? HeatmapRow,
    int? HeatmapColumn,
    string? ElementKey,
    string? ElementLabel,
    string? ElementHref);

public sealed record RecordLandingTelemetryBatchResponseDto(
    int AcceptedEvents,
    DateTime RecordedAtUtc);

public sealed record LandingGeoIpLookupResultDto(
    string Status,
    string? Provider,
    string? QueryIp,
    string? Country,
    string? CountryCode,
    string? Region,
    string? RegionCode,
    string? City);

public sealed record LandingTelemetrySessionEventDto(
    Guid Id,
    LandingTelemetryEventType EventType,
    DateTime OccurredAtUtc,
    int? ActiveSeconds,
    int? ScrollDepthPercent,
    double? ClickXPercent,
    double? ClickYPercent,
    int? HeatmapRow,
    int? HeatmapColumn,
    string? ElementKey,
    string? ElementLabel,
    string? ElementHref,
    string? MetadataJson);
