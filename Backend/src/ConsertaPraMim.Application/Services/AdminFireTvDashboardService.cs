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
        CancellationToken cancellationToken = default)
    {
        var config = await _runtimeSettings.GetConfigAsync(cancellationToken);
        var selectedRangeDays = ResolveRangeDays(config, rangeDays);
        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.AddDays(-selectedRangeDays);

        var overview = await _adminLandingAnalyticsService.GetOverviewAsync(
            new AdminLandingAnalyticsQueryDto(
                SearchTerm: null,
                Origin: null,
                Path: null,
                CountryCode: null,
                Region: null,
                City: null,
                FromUtc: fromUtc,
                ToUtc: toUtc,
                Page: 1,
                PageSize: config.SessionPageSize),
            cancellationToken);

        var geoCoverageRatePercent = overview.TotalSessions == 0
            ? 0d
            : Math.Round((overview.SessionsWithGeo * 100d) / overview.TotalSessions, 1, MidpointRounding.AwayFromZero);

        var kpis = config.KpiKeys
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(key => BuildKpi(key, overview, geoCoverageRatePercent))
            .Where(item => item != null)
            .Cast<AdminFireTvDashboardKpiDto>()
            .ToList();

        var topLocalities = overview.Sessions
            .GroupBy(item => string.IsNullOrWhiteSpace(item.EstimatedLocality) ? "Nao mapeado" : item.EstimatedLocality.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new AdminLandingAnalyticsBreakdownItemDto(group.Key, group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Label)
            .Take(config.TopListSize)
            .ToList();

        var recentSessions = overview.Sessions
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
            config.AllowedRangeDays,
            config.AutoRefreshSeconds,
            DateTime.UtcNow,
            overview.FromUtc,
            overview.ToUtc,
            kpis,
            config.ShowHeatmap ? overview.HeatmapRows : 0,
            config.ShowHeatmap ? overview.HeatmapColumns : 0,
            config.ShowHeatmap ? overview.Heatmap : [],
            overview.OriginBreakdown.Take(config.TopListSize).ToList(),
            topLocalities,
            recentSessions);
    }

    private static int ResolveRangeDays(FireTvDashboardRuntimeConfigDto config, int? requestedRangeDays)
    {
        if (requestedRangeDays.HasValue && config.AllowedRangeDays.Contains(requestedRangeDays.Value))
        {
            return requestedRangeDays.Value;
        }

        return config.DefaultRangeDays;
    }

    private static AdminFireTvDashboardKpiDto? BuildKpi(
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
            "totalSessions" => new AdminFireTvDashboardKpiDto(key, meta.Label, overview.TotalSessions.ToString("N0"), "Periodo selecionado", meta.Tone),
            "uniqueVisitors" => new AdminFireTvDashboardKpiDto(key, meta.Label, overview.TotalUniqueVisitors.ToString("N0"), "Visitantes distintos", meta.Tone),
            "leadSubmissions" => new AdminFireTvDashboardKpiDto(key, meta.Label, overview.LeadSubmissions.ToString("N0"), "Formularios enviados", meta.Tone),
            "leadSubmissionRatePercent" => new AdminFireTvDashboardKpiDto(key, meta.Label, $"{overview.LeadSubmissionRatePercent:0.0}%", "Leads / sessoes", meta.Tone),
            "leadModalOpens" => new AdminFireTvDashboardKpiDto(key, meta.Label, overview.LeadModalOpens.ToString("N0"), "Intencao de cadastro", meta.Tone),
            "totalClicks" => new AdminFireTvDashboardKpiDto(key, meta.Label, overview.TotalClicks.ToString("N0"), "Cliques rastreados", meta.Tone),
            "averageActiveSecondsPerSession" => new AdminFireTvDashboardKpiDto(key, meta.Label, $"{overview.AverageActiveSecondsPerSession:0.0}s", "Tempo engajado", meta.Tone),
            "averageMaxScrollPercent" => new AdminFireTvDashboardKpiDto(key, meta.Label, $"{overview.AverageMaxScrollPercent:0.0}%", "Profundidade media", meta.Tone),
            "sessionsWithGeoRatePercent" => new AdminFireTvDashboardKpiDto(key, meta.Label, $"{geoCoverageRatePercent:0.0}%", "GeoIP resolvido", meta.Tone),
            _ => null
        };
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
}
