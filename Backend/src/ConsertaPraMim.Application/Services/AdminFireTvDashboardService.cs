using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;

namespace ConsertaPraMim.Application.Services;

public sealed class AdminFireTvDashboardService : IAdminFireTvDashboardService
{
    private static readonly Dictionary<string, (string Label, string Tone)> SupportedKpis = new(StringComparer.OrdinalIgnoreCase)
    {
        ["totalSessions"] = ("Sessoes", "primary"),
        ["uniqueVisitors"] = ("Visitantes unicos", "neutral"),
        ["leadSubmissions"] = ("Leads captados", "success"),
        ["leadSubmissionRatePercent"] = ("Conversao", "accent"),
        ["leadModalOpens"] = ("Modais abertos", "warning"),
        ["totalClicks"] = ("Cliques totais", "neutral"),
        ["averageActiveSecondsPerSession"] = ("Tempo engajado medio", "primary"),
        ["averageMaxScrollPercent"] = ("Scroll medio", "accent"),
        ["sessionsWithGeoRatePercent"] = ("GeoIP resolvido", "success")
    };

    private readonly IAdminLandingAnalyticsService _adminLandingAnalyticsService;
    private readonly IFireTvDashboardRuntimeSettings _runtimeSettings;

    public AdminFireTvDashboardService(
        IAdminLandingAnalyticsService adminLandingAnalyticsService,
        IFireTvDashboardRuntimeSettings runtimeSettings)
    {
        _adminLandingAnalyticsService = adminLandingAnalyticsService;
        _runtimeSettings = runtimeSettings;
    }

    public async Task<AdminFireTvLandingDashboardDto> GetLandingDashboardAsync(
        int? rangeDays = null,
        string? origin = null,
        string? comparisonMode = null,
        CancellationToken cancellationToken = default)
    {
        var config = await _runtimeSettings.GetConfigAsync(cancellationToken);
        var selectedRangeDays = ResolveRangeDays(config, rangeDays);
        var selectedOrigin = ResolveOption(origin, config.OriginFilters, config.DefaultOriginFilter);
        var selectedComparisonMode = config.ShowComparison
            ? ResolveOption(comparisonMode, config.ComparisonModes, config.DefaultComparisonMode)
            : "none";

        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.AddDays(-selectedRangeDays);

        var currentInsights = await _adminLandingAnalyticsService.GetInsightsAsync(
            BuildAnalyticsQuery(fromUtc, toUtc, selectedOrigin, config.SessionPageSize),
            cancellationToken);

        var comparisonWindow = ResolveComparisonWindow(selectedComparisonMode, fromUtc, toUtc);
        AdminLandingAnalyticsInsightsDto? comparisonInsights = null;
        if (comparisonWindow != null)
        {
            comparisonInsights = await _adminLandingAnalyticsService.GetInsightsAsync(
                BuildAnalyticsQuery(comparisonWindow.FromUtc, comparisonWindow.ToUtc, selectedOrigin, config.SessionPageSize),
                cancellationToken);
        }

        var currentOverview = currentInsights.Overview;
        var comparisonOverview = comparisonInsights?.Overview;
        var currentGeoCoverageRatePercent = CalculateRatePercent(currentOverview.SessionsWithGeo, currentOverview.TotalSessions);
        var comparisonGeoCoverageRatePercent = comparisonOverview == null
            ? 0d
            : CalculateRatePercent(comparisonOverview.SessionsWithGeo, comparisonOverview.TotalSessions);

        var kpis = config.KpiKeys
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(key => BuildKpi(
                key,
                currentOverview,
                currentGeoCoverageRatePercent,
                comparisonOverview,
                comparisonGeoCoverageRatePercent,
                comparisonWindow?.Label))
            .Where(item => item != null)
            .Cast<AdminFireTvDashboardKpiDto>()
            .ToList();

        var topLocalities = currentOverview.Sessions
            .GroupBy(item => string.IsNullOrWhiteSpace(item.EstimatedLocality) ? "Nao mapeado" : item.EstimatedLocality.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new AdminLandingAnalyticsBreakdownItemDto(group.Key, group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Label)
            .Take(config.TopListSize)
            .ToList();

        var recentSessions = currentOverview.Sessions
            .Take(config.SessionPageSize)
            .Select(item => new AdminFireTvDashboardSessionDto(
                item.SessionId,
                item.Path,
                item.EstimatedLocality,
                FormatRelativeUtc(item.LastActivityAtUtc),
                item.LeadId.HasValue ? "Lead captado" : "Sem lead",
                item.ActiveSeconds,
                item.MaxScrollPercent))
            .ToList();

        return new AdminFireTvLandingDashboardDto(
            config.Enabled,
            config.AppTitle,
            config.AppSubtitle,
            selectedRangeDays,
            selectedOrigin,
            selectedComparisonMode,
            config.AllowedRangeDays,
            MapFilterOptions(config.OriginFilters),
            config.ShowComparison ? MapFilterOptions(config.ComparisonModes) : [],
            config.ShowComparison,
            config.AutoRefreshSeconds,
            DateTime.UtcNow,
            currentOverview.FromUtc,
            currentOverview.ToUtc,
            comparisonWindow?.FromUtc,
            comparisonWindow?.ToUtc,
            comparisonWindow?.Label,
            kpis,
            config.ShowHeatmap,
            config.ShowHeatmap ? currentOverview.HeatmapRows : 0,
            config.ShowHeatmap ? currentOverview.HeatmapColumns : 0,
            config.ShowHeatmap ? currentOverview.Heatmap : [],
            config.ShowScrollmap,
            config.ShowScrollmap
                ? currentInsights.Scrollmap.Select(item => new AdminFireTvDashboardScrollmapBucketDto(item.MilestonePercent, item.SessionsReached, item.SessionReachRatePercent)).ToList()
                : [],
            config.ShowElementRanking,
            config.ShowElementRanking
                ? currentInsights.ElementRanking
                    .Take(config.ElementRankingSize)
                    .Select(item => new AdminFireTvDashboardElementRankingItemDto(item.ElementKey, item.Label, item.Href, item.Clicks, item.UniqueSessions, item.SessionRatePercent))
                    .ToList()
                : [],
            currentOverview.OriginBreakdown.Take(config.TopListSize).ToList(),
            topLocalities,
            recentSessions);
    }

    private static AdminLandingAnalyticsQueryDto BuildAnalyticsQuery(
        DateTime fromUtc,
        DateTime toUtc,
        string selectedOrigin,
        int pageSize)
    {
        return new AdminLandingAnalyticsQueryDto(
            SearchTerm: null,
            Origin: string.Equals(selectedOrigin, "all", StringComparison.OrdinalIgnoreCase) ? null : selectedOrigin,
            Path: null,
            CountryCode: null,
            Region: null,
            City: null,
            FromUtc: fromUtc,
            ToUtc: toUtc,
            Page: 1,
            PageSize: pageSize);
    }

    private static IReadOnlyList<AdminFireTvDashboardFilterOptionDto> MapFilterOptions(
        IReadOnlyList<FireTvDashboardFilterOptionConfigDto> options)
    {
        return options
            .Select(item => new AdminFireTvDashboardFilterOptionDto(item.Value, item.Label))
            .ToList();
    }

    private static ComparisonWindow? ResolveComparisonWindow(
        string comparisonMode,
        DateTime fromUtc,
        DateTime toUtc)
    {
        if (!string.Equals(comparisonMode, "previous_period", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var currentWindow = toUtc - fromUtc;
        var days = Math.Max(1, (int)Math.Round(currentWindow.TotalDays, MidpointRounding.AwayFromZero));
        var comparisonToUtc = fromUtc.AddTicks(-1);
        return new ComparisonWindow(
            comparisonMode,
            $"vs {days} dia(s) anteriores",
            comparisonToUtc - currentWindow,
            comparisonToUtc);
    }

    private static int ResolveRangeDays(FireTvDashboardRuntimeConfigDto config, int? requestedRangeDays)
    {
        if (requestedRangeDays.HasValue && config.AllowedRangeDays.Contains(requestedRangeDays.Value))
        {
            return requestedRangeDays.Value;
        }

        return config.DefaultRangeDays;
    }

    private static string ResolveOption(
        string? requestedValue,
        IReadOnlyList<FireTvDashboardFilterOptionConfigDto> options,
        string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(requestedValue)
            ? fallback
            : requestedValue.Trim();

        return options.Any(item => string.Equals(item.Value, normalized, StringComparison.OrdinalIgnoreCase))
            ? normalized
            : fallback;
    }

    private static AdminFireTvDashboardKpiDto? BuildKpi(
        string key,
        AdminLandingAnalyticsOverviewDto currentOverview,
        double currentGeoCoverageRatePercent,
        AdminLandingAnalyticsOverviewDto? comparisonOverview,
        double comparisonGeoCoverageRatePercent,
        string? comparisonLabel)
    {
        var currentMetric = BuildMetricSnapshot(key, currentOverview, currentGeoCoverageRatePercent);
        if (currentMetric == null)
        {
            return null;
        }

        var comparisonMetric = comparisonOverview == null
            ? null
            : BuildMetricSnapshot(key, comparisonOverview, comparisonGeoCoverageRatePercent);

        var comparisonValue = BuildComparisonValue(currentMetric.NumericValue, comparisonMetric?.NumericValue);
        var comparisonTone = ResolveComparisonTone(comparisonValue);

        return new AdminFireTvDashboardKpiDto(
            key,
            currentMetric.Label,
            currentMetric.DisplayValue,
            currentMetric.HelperText,
            currentMetric.Tone,
            comparisonMetric?.DisplayValue,
            comparisonValue,
            comparisonMetric == null ? null : comparisonLabel,
            comparisonMetric == null ? null : comparisonTone);
    }

    private static KpiMetricSnapshot? BuildMetricSnapshot(
        string key,
        AdminLandingAnalyticsOverviewDto overview,
        double geoCoverageRatePercent)
    {
        if (!SupportedKpis.TryGetValue(key, out var meta))
        {
            return null;
        }

        return key switch
        {
            "totalSessions" => new KpiMetricSnapshot(meta.Label, meta.Tone, overview.TotalSessions, overview.TotalSessions.ToString("N0"), "Periodo selecionado"),
            "uniqueVisitors" => new KpiMetricSnapshot(meta.Label, meta.Tone, overview.TotalUniqueVisitors, overview.TotalUniqueVisitors.ToString("N0"), "Visitantes distintos"),
            "leadSubmissions" => new KpiMetricSnapshot(meta.Label, meta.Tone, overview.LeadSubmissions, overview.LeadSubmissions.ToString("N0"), "Formularios enviados"),
            "leadSubmissionRatePercent" => new KpiMetricSnapshot(meta.Label, meta.Tone, overview.LeadSubmissionRatePercent, $"{overview.LeadSubmissionRatePercent:0.0}%", "Leads / sessoes"),
            "leadModalOpens" => new KpiMetricSnapshot(meta.Label, meta.Tone, overview.LeadModalOpens, overview.LeadModalOpens.ToString("N0"), "Intencao de cadastro"),
            "totalClicks" => new KpiMetricSnapshot(meta.Label, meta.Tone, overview.TotalClicks, overview.TotalClicks.ToString("N0"), "Cliques rastreados"),
            "averageActiveSecondsPerSession" => new KpiMetricSnapshot(meta.Label, meta.Tone, overview.AverageActiveSecondsPerSession, $"{overview.AverageActiveSecondsPerSession:0.0}s", "Tempo engajado"),
            "averageMaxScrollPercent" => new KpiMetricSnapshot(meta.Label, meta.Tone, overview.AverageMaxScrollPercent, $"{overview.AverageMaxScrollPercent:0.0}%", "Profundidade media"),
            "sessionsWithGeoRatePercent" => new KpiMetricSnapshot(meta.Label, meta.Tone, geoCoverageRatePercent, $"{geoCoverageRatePercent:0.0}%", "GeoIP resolvido"),
            _ => null
        };
    }

    private static double CalculateRatePercent(int numerator, int denominator)
    {
        return denominator <= 0
            ? 0d
            : Math.Round((numerator * 100d) / denominator, 1, MidpointRounding.AwayFromZero);
    }

    private static string? BuildComparisonValue(double currentValue, double? comparisonValue)
    {
        if (!comparisonValue.HasValue)
        {
            return null;
        }

        if (Math.Abs(comparisonValue.Value) < 0.001d)
        {
            if (Math.Abs(currentValue) < 0.001d)
            {
                return "0,0%";
            }

            return "novo";
        }

        var deltaPercent = ((currentValue - comparisonValue.Value) / comparisonValue.Value) * 100d;
        return $"{deltaPercent:+0.0;-0.0;0.0}%";
    }

    private static string ResolveComparisonTone(string? comparisonValue)
    {
        if (string.IsNullOrWhiteSpace(comparisonValue))
        {
            return "neutral";
        }

        if (string.Equals(comparisonValue, "novo", StringComparison.OrdinalIgnoreCase))
        {
            return "success";
        }

        return comparisonValue.StartsWith("-", StringComparison.Ordinal)
            ? "danger"
            : comparisonValue.StartsWith("+", StringComparison.Ordinal)
                ? "success"
                : "neutral";
    }

    private static string FormatRelativeUtc(DateTime value)
    {
        var delta = DateTime.UtcNow - value;
        if (delta.TotalMinutes < 1)
        {
            return "agora";
        }

        if (delta.TotalHours < 1)
        {
            return $"{Math.Max(1, (int)Math.Round(delta.TotalMinutes, MidpointRounding.AwayFromZero))} min";
        }

        if (delta.TotalDays < 1)
        {
            return $"{Math.Max(1, (int)Math.Round(delta.TotalHours, MidpointRounding.AwayFromZero))} h";
        }

        return $"{Math.Max(1, (int)Math.Round(delta.TotalDays, MidpointRounding.AwayFromZero))} d";
    }

    private sealed record KpiMetricSnapshot(
        string Label,
        string Tone,
        double NumericValue,
        string DisplayValue,
        string HelperText);

    private sealed record ComparisonWindow(
        string Mode,
        string Label,
        DateTime FromUtc,
        DateTime ToUtc);
}
