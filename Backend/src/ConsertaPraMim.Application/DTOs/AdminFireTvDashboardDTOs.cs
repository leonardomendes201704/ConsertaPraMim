namespace ConsertaPraMim.Application.DTOs;

public sealed class FireTvDashboardRuntimeConfigDto
{
    public bool Enabled { get; init; } = true;
    public string AppTitle { get; init; } = "ConsertaPraMim Analytics TV";
    public string AppSubtitle { get; init; } = "Landing publica";
    public int DefaultRangeDays { get; init; } = 7;
    public IReadOnlyList<int> AllowedRangeDays { get; init; } = [1, 7, 30];
    public int AutoRefreshSeconds { get; init; } = 30;
    public int SessionPageSize { get; init; } = 6;
    public int TopListSize { get; init; } = 5;
    public bool ShowHeatmap { get; init; } = true;
    public IReadOnlyList<string> KpiKeys { get; init; } =
    [
        "totalSessions",
        "uniqueVisitors",
        "leadSubmissions",
        "leadSubmissionRatePercent",
        "leadModalOpens",
        "totalClicks",
        "averageActiveSecondsPerSession",
        "averageMaxScrollPercent"
    ];
}

public sealed record AdminFireTvDashboardKpiDto(
    string Key,
    string Label,
    string Value,
    string? HelperText,
    string Tone);

public sealed record AdminFireTvDashboardSessionDto(
    string SessionId,
    string Path,
    string EstimatedLocality,
    string LastActivityLabel,
    string LeadStatusLabel,
    int ActiveSeconds,
    int MaxScrollPercent);

public sealed record AdminFireTvLandingDashboardDto(
    bool Enabled,
    string AppTitle,
    string AppSubtitle,
    int SelectedRangeDays,
    IReadOnlyList<int> AllowedRangeDays,
    int AutoRefreshSeconds,
    DateTime GeneratedAtUtc,
    DateTime FromUtc,
    DateTime ToUtc,
    IReadOnlyList<AdminFireTvDashboardKpiDto> Kpis,
    int HeatmapRows,
    int HeatmapColumns,
    IReadOnlyList<AdminLandingAnalyticsHeatmapCellDto> Heatmap,
    IReadOnlyList<AdminLandingAnalyticsBreakdownItemDto> TopOrigins,
    IReadOnlyList<AdminLandingAnalyticsBreakdownItemDto> TopLocalities,
    IReadOnlyList<AdminFireTvDashboardSessionDto> RecentSessions);
