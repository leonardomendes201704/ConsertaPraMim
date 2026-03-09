namespace ConsertaPraMim.Application.DTOs;

public sealed record FireTvDashboardFilterOptionConfigDto(
    string Value,
    string Label);

public sealed record FireTvDashboardHealthTargetConfigDto(
    string Key,
    string Label,
    string Url);

public sealed class FireTvDashboardRuntimeConfigDto
{
    public bool Enabled { get; init; } = true;
    public string AppTitle { get; init; } = "ConsertaPraMim Analytics TV";
    public string AppSubtitle { get; init; } = "Landing publica";
    public bool ShowLandingView { get; init; } = true;
    public bool ShowOperationsView { get; init; } = true;
    public string DefaultView { get; init; } = "menu";
    public int DefaultRangeDays { get; init; } = 7;
    public IReadOnlyList<int> AllowedRangeDays { get; init; } = [1, 7, 30];
    public string DefaultOriginFilter { get; init; } = "all";
    public IReadOnlyList<FireTvDashboardFilterOptionConfigDto> OriginFilters { get; init; } =
    [
        new("all", "Todas as origens"),
        new("client", "Cliente"),
        new("provider", "Prestador")
    ];
    public string DefaultComparisonMode { get; init; } = "previous_period";
    public IReadOnlyList<FireTvDashboardFilterOptionConfigDto> ComparisonModes { get; init; } =
    [
        new("none", "Sem comparacao"),
        new("previous_period", "Periodo anterior")
    ];
    public int AutoRefreshSeconds { get; init; } = 30;
    public int SessionPageSize { get; init; } = 6;
    public int TopListSize { get; init; } = 5;
    public int OperationsHistoryDays { get; init; } = 7;
    public int OperationsRefreshSeconds { get; init; } = 5;
    public int SignalRPulseSeconds { get; init; } = 5;
    public int OperationsMapMaxProviders { get; init; } = 12;
    public int OperationsMapMaxRequests { get; init; } = 12;
    public int OperationsRecentActivitySize { get; init; } = 6;
    public int OperationsHealthCheckTimeoutMs { get; init; } = 2500;
    public bool ShowHeatmap { get; init; } = true;
    public bool ShowComparison { get; init; } = true;
    public bool ShowScrollmap { get; init; } = true;
    public bool ShowElementRanking { get; init; } = true;
    public int ElementRankingSize { get; init; } = 6;
    public IReadOnlyList<FireTvDashboardHealthTargetConfigDto> HealthTargets { get; init; } =
    [
        new("api", "API", "https://api.consertapramim.com/health"),
        new("admin", "Portal Admin", "https://admin.consertapramim.com/Account/Login"),
        new("client", "Portal Cliente", "https://cliente.consertapramim.com/Account/Login"),
        new("provider", "Portal Prestador", "https://prestador.consertapramim.com/Account/Login")
    ];
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
    string Tone,
    string? PreviousValue,
    string? ComparisonValue,
    string? ComparisonLabel,
    string? ComparisonTone);

public sealed record AdminFireTvDashboardFilterOptionDto(
    string Value,
    string Label);

public sealed record AdminFireTvDashboardSessionDto(
    string SessionId,
    string Path,
    string EstimatedLocality,
    string LastActivityLabel,
    string LeadStatusLabel,
    int ActiveSeconds,
    int MaxScrollPercent);

public sealed record AdminFireTvDashboardScrollmapBucketDto(
    int MilestonePercent,
    int SessionsReached,
    double SessionReachRatePercent);

public sealed record AdminFireTvDashboardElementRankingItemDto(
    string ElementKey,
    string Label,
    string? Href,
    int Clicks,
    int UniqueSessions,
    double SessionRatePercent);

public sealed record AdminFireTvHealthTargetStatusDto(
    string Key,
    string Label,
    string Url,
    bool Healthy,
    int? LatencyMs,
    string StatusLabel,
    string? Detail);

public sealed record AdminFireTvOperationalCategoryDto(
    string Category,
    int Count,
    double Percent);

public sealed record AdminFireTvOperationalMapPointDto(
    Guid Id,
    string PointType,
    string Label,
    string Subtitle,
    double Latitude,
    double Longitude,
    string Tone);

public sealed record AdminFireTvOperationalDailySeriesItemDto(
    string Label,
    int Requests,
    int Attendances);

public sealed record AdminFireTvOperationalRecentActivityDto(
    string TimeLabel,
    string Title,
    string Subtitle,
    string Tone);

public sealed record AdminFireTvLandingDashboardDto(
    bool Enabled,
    string AppTitle,
    string AppSubtitle,
    int SelectedRangeDays,
    string SelectedOrigin,
    string SelectedComparisonMode,
    IReadOnlyList<int> AllowedRangeDays,
    IReadOnlyList<AdminFireTvDashboardFilterOptionDto> OriginOptions,
    IReadOnlyList<AdminFireTvDashboardFilterOptionDto> ComparisonOptions,
    bool ShowComparison,
    int AutoRefreshSeconds,
    DateTime GeneratedAtUtc,
    DateTime FromUtc,
    DateTime ToUtc,
    DateTime? ComparisonFromUtc,
    DateTime? ComparisonToUtc,
    string? ComparisonLabel,
    IReadOnlyList<AdminFireTvDashboardKpiDto> Kpis,
    bool ShowHeatmap,
    int HeatmapRows,
    int HeatmapColumns,
    IReadOnlyList<AdminLandingAnalyticsHeatmapCellDto> Heatmap,
    bool ShowScrollmap,
    IReadOnlyList<AdminFireTvDashboardScrollmapBucketDto> Scrollmap,
    bool ShowElementRanking,
    IReadOnlyList<AdminFireTvDashboardElementRankingItemDto> TopElements,
    IReadOnlyList<AdminLandingAnalyticsBreakdownItemDto> TopOrigins,
    IReadOnlyList<AdminLandingAnalyticsBreakdownItemDto> TopLocalities,
    IReadOnlyList<AdminFireTvDashboardSessionDto> RecentSessions);

public sealed record AdminFireTvOperationsDashboardDto(
    bool Enabled,
    string AppTitle,
    string AppSubtitle,
    int RefreshSeconds,
    int PulseSeconds,
    int HistoryDays,
    DateTime GeneratedAtUtc,
    bool RealtimeConnected,
    string OverallStatus,
    int? AverageLatencyMs,
    int HealthyTargets,
    int TotalTargets,
    IReadOnlyList<AdminFireTvHealthTargetStatusDto> HealthTargets,
    IReadOnlyList<AdminFireTvDashboardKpiDto> Kpis,
    IReadOnlyList<AdminFireTvOperationalCategoryDto> Categories,
    IReadOnlyList<AdminFireTvOperationalMapPointDto> ProviderPoints,
    IReadOnlyList<AdminFireTvOperationalMapPointDto> RequestPoints,
    IReadOnlyList<AdminFireTvOperationalDailySeriesItemDto> DailySeries,
    IReadOnlyList<AdminFireTvOperationalRecentActivityDto> RecentActivity);
