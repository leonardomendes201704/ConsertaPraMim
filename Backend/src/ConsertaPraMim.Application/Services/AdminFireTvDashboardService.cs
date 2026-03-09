using System.Globalization;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

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
    private readonly IAdminDashboardService _adminDashboardService;
    private readonly IFireTvDashboardRuntimeSettings _runtimeSettings;
    private readonly IFireTvDashboardHealthProbe _healthProbe;
    private readonly IUserRepository _userRepository;
    private readonly IServiceRequestRepository _serviceRequestRepository;

    public AdminFireTvDashboardService(
        IAdminLandingAnalyticsService adminLandingAnalyticsService,
        IAdminDashboardService adminDashboardService,
        IFireTvDashboardRuntimeSettings runtimeSettings,
        IFireTvDashboardHealthProbe healthProbe,
        IUserRepository userRepository,
        IServiceRequestRepository serviceRequestRepository)
    {
        _adminLandingAnalyticsService = adminLandingAnalyticsService;
        _adminDashboardService = adminDashboardService;
        _runtimeSettings = runtimeSettings;
        _healthProbe = healthProbe;
        _userRepository = userRepository;
        _serviceRequestRepository = serviceRequestRepository;
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

    public async Task<AdminFireTvOperationsDashboardDto> GetOperationsDashboardAsync(CancellationToken cancellationToken = default)
    {
        var config = await _runtimeSettings.GetConfigAsync(cancellationToken);
        var timeZone = ResolveBusinessTimeZone();
        var nowUtc = DateTime.UtcNow;
        var (todayFromUtc, todayToUtc) = ResolveTodayWindowUtc(timeZone, nowUtc);
        var historyFromUtc = nowUtc.AddDays(-config.OperationsHistoryDays);

        var dashboard = await _adminDashboardService.GetDashboardAsync(
            new AdminDashboardQueryDto(
                FromUtc: historyFromUtc,
                ToUtc: nowUtc,
                EventType: null,
                OperationalStatus: null,
                SearchTerm: null,
                Page: 1,
                PageSize: 20));

        var coverageMap = await _adminDashboardService.GetCoverageMapAsync();
        var users = (await _userRepository.GetAllAsync()).ToList();
        var requests = (await _serviceRequestRepository.GetAllAsync()).ToList();

        var healthTargets = await _healthProbe.ProbeAsync(
            config.HealthTargets,
            config.OperationsHealthCheckTimeoutMs,
            cancellationToken);

        var allAppointments = requests
            .SelectMany(item => item.Appointments)
            .ToList();

        var servicesToday = requests.Count(item => item.CreatedAt >= todayFromUtc && item.CreatedAt <= todayToUtc);
        var activeAttendances = allAppointments.Count(item => IsActiveAppointment(item.Status));
        var completedServices = requests.Count(item =>
        {
            if (item.Status is not (ServiceRequestStatus.Completed or ServiceRequestStatus.Validated))
            {
                return false;
            }

            var completedAtUtc = ResolveRequestCompletionReferenceUtc(item);
            return completedAtUtc >= todayFromUtc && completedAtUtc <= todayToUtc;
        });
        var cancelledCalls = requests.Count(item =>
            item.Status == ServiceRequestStatus.Canceled &&
            NormalizeUtc(item.UpdatedAt ?? item.CreatedAt) >= todayFromUtc &&
            NormalizeUtc(item.UpdatedAt ?? item.CreatedAt) <= todayToUtc);

        var providerProfiles = users
            .Where(item => item.Role == UserRole.Provider && item.ProviderProfile is not null)
            .Select(item => item.ProviderProfile!)
            .ToList();
        var totalReviewCount = providerProfiles.Sum(item => Math.Max(0, item.ReviewCount));
        var weightedRating = totalReviewCount == 0
            ? 0d
            : providerProfiles.Sum(item => item.Rating * Math.Max(0, item.ReviewCount)) / totalReviewCount;

        var culture = CultureInfo.GetCultureInfo("pt-BR");

        var kpis = new List<AdminFireTvDashboardKpiDto>
        {
            new(
                "servicesToday",
                "Servicos hoje",
                servicesToday.ToString("N0", culture),
                "Pedidos abertos hoje",
                "primary",
                null,
                null,
                null,
                null),
            new(
                "registeredProviders",
                "Profissionais cadastrados",
                dashboard.TotalProviders.ToString("N0", culture),
                $"{dashboard.OnlineProviders.ToString("N0", culture)} online",
                "success",
                null,
                null,
                null,
                null),
            new(
                "activeAttendances",
                "Atendimentos",
                activeAttendances.ToString("N0", culture),
                $"{dashboard.ActiveRequests.ToString("N0", culture)} pedidos ativos",
                "warning",
                null,
                null,
                null,
                null),
            new(
                "averageRating",
                "Avaliacao media",
                totalReviewCount == 0 ? "--" : weightedRating.ToString("0.0", culture),
                totalReviewCount == 0 ? "Sem avaliacoes" : $"{totalReviewCount.ToString("N0", culture)} avaliacoes",
                "accent",
                null,
                null,
                null,
                null),
            new(
                "completedServices",
                "Servicos concluidos",
                completedServices.ToString("N0", culture),
                "Concluidos hoje",
                "success",
                null,
                null,
                null,
                null),
            new(
                "sla",
                "SLA",
                $"{dashboard.AppointmentConfirmationInSlaRatePercent.ToString("N1", culture)}%",
                "Confirmacao no SLA",
                "primary",
                null,
                null,
                null,
                null),
            new(
                "monthlySubscriptionRevenue",
                "Receita mensal",
                dashboard.MonthlySubscriptionRevenue.ToString("C0", culture),
                "Assinaturas dos prestadores",
                "accent",
                null,
                null,
                null,
                null),
            new(
                "cancelledCalls",
                "Chamados cancelados",
                cancelledCalls.ToString("N0", culture),
                "Cancelados hoje",
                "danger",
                null,
                null,
                null,
                null)
        };

        var totalCategoryCount = dashboard.RequestsByCategory.Sum(item => item.Count);
        var categories = dashboard.RequestsByCategory
            .Where(item => item.Count > 0)
            .OrderByDescending(item => item.Count)
            .Take(6)
            .Select(item => new AdminFireTvOperationalCategoryDto(
                item.Category,
                item.Count,
                totalCategoryCount <= 0
                    ? 0d
                    : Math.Round(item.Count * 100d / totalCategoryCount, 1, MidpointRounding.AwayFromZero)))
            .ToList();

        var providerPoints = coverageMap.Providers
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.ProviderName)
            .Take(config.OperationsMapMaxProviders)
            .Select(item => new AdminFireTvOperationalMapPointDto(
                item.ProviderId,
                "provider",
                item.ProviderName,
                string.IsNullOrWhiteSpace(item.City)
                    ? FormatOperationalStatus(item.OperationalStatus)
                    : $"{item.City} • {FormatOperationalStatus(item.OperationalStatus)}",
                item.Latitude,
                item.Longitude,
                ResolveProviderPointTone(item.OperationalStatus, item.IsActive)))
            .ToList();

        var requestPoints = coverageMap.Requests
            .Where(item => !string.Equals(item.Status, ServiceRequestStatus.Canceled.ToString(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(config.OperationsMapMaxRequests)
            .Select(item => new AdminFireTvOperationalMapPointDto(
                item.RequestId,
                "request",
                item.Category,
                BuildRequestLocationLabel(item),
                item.Latitude,
                item.Longitude,
                ResolveRequestPointTone(item.Status)))
            .ToList();

        var dailySeries = BuildDailySeries(requests, historyFromUtc, nowUtc, timeZone);

        var recentActivity = requests
            .OrderByDescending(item => NormalizeUtc(item.UpdatedAt ?? item.CreatedAt))
            .Take(config.OperationsRecentActivitySize)
            .Select(item =>
            {
                var localTime = TimeZoneInfo.ConvertTimeFromUtc(NormalizeUtc(item.UpdatedAt ?? item.CreatedAt), timeZone);
                return new AdminFireTvOperationalRecentActivityDto(
                    ResolveCategoryIcon(item),
                    localTime.ToString("HH:mm", culture),
                    $"{ResolveCategoryName(item)} - {item.AddressCity}",
                    BuildRecentActivitySubtitle(item),
                    ResolveRequestPointTone(item.Status.ToString()));
            })
            .ToList();

        var averageLatency = healthTargets
            .Where(item => item.LatencyMs.HasValue)
            .Select(item => item.LatencyMs!.Value)
            .DefaultIfEmpty()
            .Average();
        var healthyTargets = healthTargets.Count(item => item.Healthy);
        var overallStatus = healthyTargets == healthTargets.Count
            ? "online"
            : healthyTargets == 0
                ? "offline"
                : "warning";

        return new AdminFireTvOperationsDashboardDto(
            config.Enabled,
            config.AppTitle,
            "Visao operacional",
            config.OperationsRefreshSeconds,
            config.SignalRPulseSeconds,
            config.OperationsHistoryDays,
            nowUtc,
            true,
            overallStatus,
            healthTargets.Count(item => item.LatencyMs.HasValue) == 0 ? null : (int)Math.Round(averageLatency, MidpointRounding.AwayFromZero),
            healthyTargets,
            healthTargets.Count,
            healthTargets,
            kpis,
            categories,
            providerPoints,
            requestPoints,
            dailySeries,
            recentActivity);
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

    private static IReadOnlyList<AdminFireTvOperationalDailySeriesItemDto> BuildDailySeries(
        IReadOnlyCollection<ServiceRequest> requests,
        DateTime fromUtc,
        DateTime toUtc,
        TimeZoneInfo timeZone)
    {
        var startLocalDate = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, timeZone).Date;
        var endLocalDate = TimeZoneInfo.ConvertTimeFromUtc(toUtc, timeZone).Date;
        var output = new List<AdminFireTvOperationalDailySeriesItemDto>();

        for (var cursor = startLocalDate; cursor <= endLocalDate; cursor = cursor.AddDays(1))
        {
            var requestsCount = requests.Count(item => TimeZoneInfo.ConvertTimeFromUtc(NormalizeUtc(item.CreatedAt), timeZone).Date == cursor);
            var attendancesCount = requests
                .SelectMany(item => item.Appointments)
                .Count(item => TimeZoneInfo.ConvertTimeFromUtc(NormalizeUtc(item.CreatedAt), timeZone).Date == cursor);

            output.Add(new AdminFireTvOperationalDailySeriesItemDto(
                cursor.ToString("dd/MM", CultureInfo.GetCultureInfo("pt-BR")),
                requestsCount,
                attendancesCount));
        }

        return output;
    }

    private static (DateTime FromUtc, DateTime ToUtc) ResolveTodayWindowUtc(TimeZoneInfo timeZone, DateTime nowUtc)
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone);
        var localStart = localNow.Date;
        var localEnd = localStart.AddDays(1).AddTicks(-1);
        return (
            TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone),
            TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone));
    }

    private static TimeZoneInfo ResolveBusinessTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch
        {
            return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }
    }

    private static bool IsActiveAppointment(ServiceAppointmentStatus status)
    {
        return status is ServiceAppointmentStatus.PendingProviderConfirmation
            or ServiceAppointmentStatus.Confirmed
            or ServiceAppointmentStatus.RescheduleRequestedByClient
            or ServiceAppointmentStatus.RescheduleRequestedByProvider
            or ServiceAppointmentStatus.RescheduleConfirmed
            or ServiceAppointmentStatus.Arrived
            or ServiceAppointmentStatus.InProgress;
    }

    private static string ResolveProviderPointTone(string operationalStatus, bool isActive)
    {
        if (!isActive)
        {
            return "danger";
        }

        return operationalStatus switch
        {
            nameof(ProviderOperationalStatus.Online) => "success",
            nameof(ProviderOperationalStatus.EmAtendimento) => "warning",
            _ => "neutral"
        };
    }

    private static string ResolveRequestPointTone(string status)
    {
        if (string.Equals(status, ServiceRequestStatus.Canceled.ToString(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, ServiceAppointmentStatus.CancelledByClient.ToString(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, ServiceAppointmentStatus.CancelledByProvider.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return "danger";
        }

        if (string.Equals(status, ServiceRequestStatus.Completed.ToString(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, ServiceRequestStatus.Validated.ToString(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, ServiceAppointmentStatus.Completed.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return "success";
        }

        return "warning";
    }

    private static string BuildRequestLocationLabel(AdminCoverageMapRequestDto item)
    {
        if (!string.IsNullOrWhiteSpace(item.AddressNeighborhood))
        {
            return $"{item.AddressNeighborhood} - {item.AddressCity}";
        }

        return item.AddressCity;
    }

    private static string BuildRecentActivitySubtitle(ServiceRequest request)
    {
        var status = request.Status switch
        {
            ServiceRequestStatus.Created => "Pedido aberto",
            ServiceRequestStatus.Matching => "Em matching",
            ServiceRequestStatus.Scheduled => "Agendado",
            ServiceRequestStatus.InProgress => "Em atendimento",
            ServiceRequestStatus.Completed => "Concluido",
            ServiceRequestStatus.Validated => "Validado",
            ServiceRequestStatus.PendingClientCompletionAcceptance => "Aguardando cliente",
            ServiceRequestStatus.Canceled => "Cancelado",
            _ => request.Status.ToString()
        };

        return string.IsNullOrWhiteSpace(request.AddressNeighborhood)
            ? $"{status} • {request.AddressCity}"
            : $"{status} • {request.AddressNeighborhood} - {request.AddressCity}";
    }

    private static string FormatOperationalStatus(string operationalStatus)
    {
        return operationalStatus switch
        {
            nameof(ProviderOperationalStatus.Online) => "Online",
            nameof(ProviderOperationalStatus.EmAtendimento) => "Em atendimento",
            nameof(ProviderOperationalStatus.Ausente) => "Ausente",
            _ => operationalStatus
        };
    }

    private static string ResolveCategoryName(ServiceRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.CategoryDefinition?.Name))
        {
            return request.CategoryDefinition.Name;
        }

        return request.Category.ToPtBr();
    }

    private static string ResolveCategoryIcon(ServiceRequest request)
    {
        return string.IsNullOrWhiteSpace(request.CategoryDefinition?.Icon)
            ? "build_circle"
            : request.CategoryDefinition.Icon.Trim();
    }

    private static DateTime ResolveRequestCompletionReferenceUtc(ServiceRequest request)
    {
        var completedAt = request.Appointments
            .Where(appointment => appointment.CompletedAtUtc.HasValue)
            .Select(appointment => appointment.CompletedAtUtc!.Value)
            .OrderByDescending(value => value)
            .FirstOrDefault();

        if (completedAt != default)
        {
            return NormalizeUtc(completedAt);
        }

        return NormalizeUtc(request.UpdatedAt ?? request.CreatedAt);
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
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

