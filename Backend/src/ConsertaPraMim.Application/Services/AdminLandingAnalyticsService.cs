using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public sealed class AdminLandingAnalyticsService : IAdminLandingAnalyticsService
{
    private readonly ILandingAccessEventRepository _landingAccessEventRepository;
    private readonly ILandingTelemetryEventRepository _landingTelemetryEventRepository;
    private readonly ILandingLeadRepository _landingLeadRepository;
    private readonly ILandingAnalyticsRuntimeSettings _landingAnalyticsRuntimeSettings;

    public AdminLandingAnalyticsService(
        ILandingAccessEventRepository landingAccessEventRepository,
        ILandingTelemetryEventRepository landingTelemetryEventRepository,
        ILandingLeadRepository landingLeadRepository,
        ILandingAnalyticsRuntimeSettings landingAnalyticsRuntimeSettings)
    {
        _landingAccessEventRepository = landingAccessEventRepository;
        _landingTelemetryEventRepository = landingTelemetryEventRepository;
        _landingLeadRepository = landingLeadRepository;
        _landingAnalyticsRuntimeSettings = landingAnalyticsRuntimeSettings;
    }

    public async Task<AdminLandingAnalyticsOverviewDto> GetOverviewAsync(
        AdminLandingAnalyticsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var normalizedFromUtc = query.FromUtc ?? DateTime.UtcNow.AddDays(-30);
        var normalizedToUtc = query.ToUtc ?? DateTime.UtcNow;
        if (normalizedFromUtc > normalizedToUtc)
        {
            (normalizedFromUtc, normalizedToUtc) = (normalizedToUtc, normalizedFromUtc);
        }

        var runtimeConfig = await _landingAnalyticsRuntimeSettings.GetConfigAsync(cancellationToken);
        var accessEvents = await _landingAccessEventRepository.GetByPeriodAsync(normalizedFromUtc, normalizedToUtc, cancellationToken);
        var telemetryEvents = await _landingTelemetryEventRepository.GetByPeriodAsync(normalizedFromUtc, normalizedToUtc, cancellationToken);
        var leads = await _landingLeadRepository.GetByPeriodAsync(normalizedFromUtc, normalizedToUtc, cancellationToken);

        var sessions = BuildSessions(accessEvents, telemetryEvents, leads);
        var filteredSessions = ApplyFilters(sessions, query);
        var sessionIds = filteredSessions
            .Select(item => item.SessionId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var filteredTelemetryEvents = telemetryEvents
            .Where(item => sessionIds.Contains(item.SessionId))
            .ToList();

        var totalSessions = filteredSessions.Count;
        var totalUniqueVisitors = filteredSessions
            .Select(item => item.VisitorId)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var sessionsWithGeo = filteredSessions.Count(item => item.HasGeo);
        var totalHeartbeats = filteredTelemetryEvents.Count(item => item.EventType == LandingTelemetryEventType.Heartbeat);
        var totalActiveSeconds = filteredSessions.Sum(item => item.ActiveSeconds);
        var averageActiveSecondsPerSession = totalSessions == 0
            ? 0d
            : Math.Round(totalActiveSeconds / (double)totalSessions, 1, MidpointRounding.AwayFromZero);
        var averageMaxScrollPercent = totalSessions == 0
            ? 0d
            : Math.Round(filteredSessions.Average(item => item.MaxScrollPercent), 1, MidpointRounding.AwayFromZero);
        var sessionsWithClicks = filteredSessions.Count(item => item.Clicks > 0);
        var totalClicks = filteredSessions.Sum(item => item.Clicks);
        var leadModalOpens = filteredSessions.Sum(item => item.ModalOpens);
        var leadSubmissions = filteredSessions.Sum(item => item.LeadSubmissions);
        var leadSubmissionRatePercent = totalSessions == 0
            ? 0d
            : Math.Round((leadSubmissions * 100d) / totalSessions, 1, MidpointRounding.AwayFromZero);

        var orderedSessions = filteredSessions
            .OrderByDescending(item => item.LastActivityAtUtc)
            .ThenByDescending(item => item.StartedAtUtc)
            .ToList();
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var pagedSessions = orderedSessions
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new AdminLandingAnalyticsSessionListItemDto(
                item.SessionId,
                item.VisitorId,
                item.StartedAtUtc,
                item.LastActivityAtUtc,
                item.Path,
                item.InitialLeadOrigin,
                item.EstimatedLocality,
                item.ActiveSeconds,
                item.MaxScrollPercent,
                item.Clicks,
                item.ModalOpens,
                item.LeadSubmissions,
                item.Lead?.Id))
            .ToList();

        return new AdminLandingAnalyticsOverviewDto(
            normalizedFromUtc,
            normalizedToUtc,
            totalSessions,
            totalUniqueVisitors,
            sessionsWithGeo,
            totalHeartbeats,
            totalActiveSeconds,
            averageActiveSecondsPerSession,
            averageMaxScrollPercent,
            sessionsWithClicks,
            totalClicks,
            leadModalOpens,
            leadSubmissions,
            leadSubmissionRatePercent,
            runtimeConfig.Clicks.HeatmapGridRows,
            runtimeConfig.Clicks.HeatmapGridColumns,
            page,
            pageSize,
            totalSessions,
            BuildBreakdown(filteredSessions, item => item.Path, "Pagina nao informada"),
            BuildBreakdown(filteredSessions, item => ResolveOriginLabel(item.InitialLeadOrigin), "Origem nao informada"),
            BuildBreakdown(filteredSessions, item => FirstNonEmpty(item.AccessEvent.GeoCountryCode, item.AccessEvent.GeoCountry), "Pais nao mapeado"),
            BuildBreakdown(filteredSessions, item => FirstNonEmpty(item.AccessEvent.GeoRegionCode, item.AccessEvent.GeoRegion), "UF/Regiao nao mapeada"),
            BuildBreakdown(filteredSessions, item => item.AccessEvent.GeoCity, "Cidade nao mapeada"),
            BuildEventBreakdown(filteredTelemetryEvents, totalSessions),
            BuildHeatmap(filteredTelemetryEvents),
            pagedSessions);
    }

    public async Task<AdminLandingAnalyticsSessionDetailsDto?> GetSessionDetailsAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        var normalizedSessionId = sessionId.Trim();
        var accessEvent = await _landingAccessEventRepository.GetBySessionIdAsync(normalizedSessionId, cancellationToken);
        if (accessEvent == null)
        {
            return null;
        }

        var telemetryEvents = await _landingTelemetryEventRepository.GetBySessionIdAsync(normalizedSessionId, cancellationToken);
        var lead = await _landingLeadRepository.GetBySessionIdAsync(normalizedSessionId, cancellationToken);
        var session = BuildSession(accessEvent, telemetryEvents, lead);

        return new AdminLandingAnalyticsSessionDetailsDto(
            session.SessionId,
            session.VisitorId,
            session.StartedAtUtc,
            session.LastActivityAtUtc,
            session.Path,
            session.AccessEvent.CurrentUrl,
            session.InitialLeadOrigin,
            session.ActiveSeconds,
            session.MaxScrollPercent,
            session.Clicks,
            session.ModalOpens,
            session.LeadSubmissions,
            session.EstimatedLocality,
            session.AccessEvent.IpAddress,
            session.AccessEvent.ForwardedFor,
            session.AccessEvent.UserAgent,
            session.AccessEvent.AcceptLanguage,
            session.AccessEvent.RefererUrl,
            new AdminLandingAnalyticsSessionGeoDto(
                session.AccessEvent.GeoCountry,
                session.AccessEvent.GeoCountryCode,
                session.AccessEvent.GeoRegion,
                session.AccessEvent.GeoRegionCode,
                session.AccessEvent.GeoCity,
                session.AccessEvent.GeoProvider,
                session.AccessEvent.GeoLookupStatus),
            session.Lead == null
                ? null
                : new AdminLandingAnalyticsSessionLeadDto(
                    session.Lead.Id,
                    session.Lead.Origin,
                    session.Lead.FullName,
                    session.Lead.Email,
                    session.Lead.Phone,
                    BuildLeadLocality(session.Lead),
                    session.Lead.CreatedAt),
            BuildTimeline(session.AccessEvent, telemetryEvents, session.Lead));
    }

    private static List<LandingSessionAggregate> BuildSessions(
        IReadOnlyList<LandingAccessEvent> accessEvents,
        IReadOnlyList<LandingTelemetryEvent> telemetryEvents,
        IReadOnlyList<LandingLead> leads)
    {
        var telemetryBySession = telemetryEvents
            .Where(item => !string.IsNullOrWhiteSpace(item.SessionId))
            .GroupBy(item => item.SessionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<LandingTelemetryEvent>)group.OrderBy(item => item.OccurredAtUtc).ToList(), StringComparer.OrdinalIgnoreCase);

        var leadsBySession = leads
            .Where(item => !string.IsNullOrWhiteSpace(item.SessionId))
            .GroupBy(item => item.SessionId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.CreatedAt).First(), StringComparer.OrdinalIgnoreCase);

        var leadsByVisitor = leads
            .Where(item => !string.IsNullOrWhiteSpace(item.VisitorId))
            .GroupBy(item => item.VisitorId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.CreatedAt).ToList(), StringComparer.OrdinalIgnoreCase);

        return accessEvents
            .Where(item => !string.IsNullOrWhiteSpace(item.SessionId))
            .Select(accessEvent =>
            {
                telemetryBySession.TryGetValue(accessEvent.SessionId, out var sessionTelemetry);
                leadsBySession.TryGetValue(accessEvent.SessionId, out var sessionLead);
                sessionLead ??= ResolveLeadByVisitor(accessEvent, leadsByVisitor);
                return BuildSession(accessEvent, sessionTelemetry ?? Array.Empty<LandingTelemetryEvent>(), sessionLead);
            })
            .ToList();
    }

    private static LandingLead? ResolveLeadByVisitor(
        LandingAccessEvent accessEvent,
        IReadOnlyDictionary<string, List<LandingLead>> leadsByVisitor)
    {
        if (string.IsNullOrWhiteSpace(accessEvent.VisitorId) ||
            !leadsByVisitor.TryGetValue(accessEvent.VisitorId, out var leads))
        {
            return null;
        }

        var minCreatedAt = accessEvent.CreatedAt.AddMinutes(-5);
        var maxCreatedAt = accessEvent.CreatedAt.AddHours(12);
        return leads.FirstOrDefault(lead => lead.CreatedAt >= minCreatedAt && lead.CreatedAt <= maxCreatedAt);
    }

    private static LandingSessionAggregate BuildSession(
        LandingAccessEvent accessEvent,
        IReadOnlyList<LandingTelemetryEvent> telemetryEvents,
        LandingLead? lead)
    {
        var activeSeconds = telemetryEvents
            .Where(item => item.EventType == LandingTelemetryEventType.Heartbeat)
            .Sum(item => item.ActiveSeconds ?? 0);
        var maxScrollPercent = telemetryEvents
            .Where(item => item.EventType == LandingTelemetryEventType.ScrollMilestone)
            .Select(item => item.ScrollDepthPercent ?? 0)
            .DefaultIfEmpty(0)
            .Max();
        var clicks = telemetryEvents.Count(item => item.EventType == LandingTelemetryEventType.Click);
        var modalOpens = telemetryEvents.Count(item => item.EventType == LandingTelemetryEventType.LeadModalOpen);
        var leadSubmissions = Math.Max(
            telemetryEvents.Count(item => item.EventType == LandingTelemetryEventType.LeadSubmitSuccess),
            lead == null ? 0 : 1);

        var lastActivityAtUtc = new[]
        {
            accessEvent.CreatedAt,
            telemetryEvents.Select(item => item.OccurredAtUtc).DefaultIfEmpty(accessEvent.CreatedAt).Max(),
            lead?.CreatedAt ?? accessEvent.CreatedAt
        }.Max();

        return new LandingSessionAggregate(
            accessEvent.SessionId,
            accessEvent.VisitorId,
            accessEvent.CreatedAt,
            lastActivityAtUtc,
            NormalizePath(accessEvent.Path),
            accessEvent.InitialLeadOrigin,
            BuildEstimatedLocality(accessEvent, lead),
            activeSeconds,
            maxScrollPercent,
            clicks,
            modalOpens,
            leadSubmissions,
            HasGeo(accessEvent),
            accessEvent,
            telemetryEvents,
            lead);
    }

    private static List<LandingSessionAggregate> ApplyFilters(
        IEnumerable<LandingSessionAggregate> sessions,
        AdminLandingAnalyticsQueryDto query)
    {
        var searchTerm = NormalizeFilter(query.SearchTerm);
        var normalizedOrigin = NormalizeOriginFilter(query.Origin);
        var normalizedPath = NormalizeFilter(query.Path);
        var normalizedCountryCode = NormalizeFilter(query.CountryCode)?.ToUpperInvariant();
        var normalizedRegion = NormalizeFilter(query.Region);
        var normalizedCity = NormalizeFilter(query.City);

        return sessions
            .Where(item =>
                (normalizedOrigin == null || item.InitialLeadOrigin == normalizedOrigin) &&
                (normalizedPath == null || item.Path.Contains(normalizedPath, StringComparison.OrdinalIgnoreCase)) &&
                (normalizedCountryCode == null || string.Equals(item.AccessEvent.GeoCountryCode, normalizedCountryCode, StringComparison.OrdinalIgnoreCase)) &&
                (normalizedRegion == null || ContainsAny(item.AccessEvent.GeoRegionCode, item.AccessEvent.GeoRegion, normalizedRegion)) &&
                (normalizedCity == null || ContainsAny(item.AccessEvent.GeoCity, item.Lead?.City, normalizedCity)) &&
                (searchTerm == null || MatchesSearch(item, searchTerm)))
            .ToList();
    }

    private static bool MatchesSearch(LandingSessionAggregate item, string searchTerm)
    {
        return ContainsAny(
            item.SessionId,
            item.VisitorId,
            item.Path,
            item.EstimatedLocality,
            item.AccessEvent.GeoCountry,
            item.AccessEvent.GeoCountryCode,
            item.AccessEvent.GeoRegion,
            item.AccessEvent.GeoRegionCode,
            item.AccessEvent.GeoCity,
            item.Lead?.FullName,
            item.Lead?.Email,
            item.Lead?.Phone,
            item.Lead?.ServiceCategory,
            item.Lead?.RequestedService,
            searchTerm);
    }

    private static IReadOnlyList<AdminLandingAnalyticsBreakdownItemDto> BuildBreakdown(
        IEnumerable<LandingSessionAggregate> sessions,
        Func<LandingSessionAggregate, string?> selector,
        string fallbackLabel)
    {
        return sessions
            .GroupBy(item =>
            {
                var label = selector(item);
                return string.IsNullOrWhiteSpace(label) ? fallbackLabel : label.Trim();
            }, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AdminLandingAnalyticsBreakdownItemDto(group.Key, group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Label)
            .Take(10)
            .ToList();
    }

    private static IReadOnlyList<AdminLandingAnalyticsBreakdownItemDto> BuildEventBreakdown(
        IReadOnlyList<LandingTelemetryEvent> telemetryEvents,
        int totalSessions)
    {
        var items = telemetryEvents
            .GroupBy(item => ResolveEventLabel(item.EventType))
            .Select(group => new AdminLandingAnalyticsBreakdownItemDto(group.Key, group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Label)
            .ToList();

        if (totalSessions > 0)
        {
            items.Insert(0, new AdminLandingAnalyticsBreakdownItemDto("Acesso", totalSessions));
        }

        return items;
    }

    private static IReadOnlyList<AdminLandingAnalyticsHeatmapCellDto> BuildHeatmap(
        IReadOnlyList<LandingTelemetryEvent> telemetryEvents)
    {
        return telemetryEvents
            .Where(item =>
                item.EventType == LandingTelemetryEventType.Click &&
                item.HeatmapRow.HasValue &&
                item.HeatmapColumn.HasValue)
            .GroupBy(item => new { Row = item.HeatmapRow!.Value, Column = item.HeatmapColumn!.Value })
            .Select(group => new AdminLandingAnalyticsHeatmapCellDto(group.Key.Row, group.Key.Column, group.Count()))
            .OrderBy(item => item.Row)
            .ThenBy(item => item.Column)
            .ToList();
    }

    private static IReadOnlyList<AdminLandingAnalyticsTimelineItemDto> BuildTimeline(
        LandingAccessEvent accessEvent,
        IReadOnlyList<LandingTelemetryEvent> telemetryEvents,
        LandingLead? lead)
    {
        var timeline = new List<AdminLandingAnalyticsTimelineItemDto>
        {
            new(
                "Landing",
                "Acesso",
                accessEvent.CreatedAt,
                $"Acesso registrado em {NormalizePath(accessEvent.Path)}",
                accessEvent.MetadataJson)
        };

        timeline.AddRange(telemetryEvents.Select(item => new AdminLandingAnalyticsTimelineItemDto(
            "Landing",
            ResolveEventLabel(item.EventType),
            item.OccurredAtUtc,
            BuildTelemetryDescription(item),
            item.MetadataJson)));

        if (lead != null)
        {
            timeline.Add(new AdminLandingAnalyticsTimelineItemDto(
                "Landing",
                "Lead captado",
                lead.CreatedAt,
                $"{ResolveOriginLabel(lead.Origin)} | {lead.FullName} | {BuildLeadLocality(lead)}",
                lead.MetadataJson));
        }

        return timeline
            .OrderBy(item => item.OccurredAtUtc)
            .ToList();
    }

    private static string BuildTelemetryDescription(LandingTelemetryEvent item)
    {
        return item.EventType switch
        {
            LandingTelemetryEventType.Heartbeat => $"Heartbeat +{item.ActiveSeconds ?? 0}s",
            LandingTelemetryEventType.ScrollMilestone => $"Scroll em {item.ScrollDepthPercent ?? 0}%",
            LandingTelemetryEventType.Click => $"Clique em {FirstNonEmpty(item.ElementLabel, item.ElementKey, item.ElementHref) ?? "elemento"}",
            LandingTelemetryEventType.LeadModalOpen => $"Modal de lead aberto ({ResolveOriginLabel(item.InitialLeadOrigin)})",
            LandingTelemetryEventType.LeadSubmitSuccess => $"Lead enviado com sucesso ({ResolveOriginLabel(item.InitialLeadOrigin)})",
            _ => "Evento da landing"
        };
    }

    private static string NormalizePath(string? path)
        => string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();

    private static string BuildEstimatedLocality(LandingAccessEvent accessEvent, LandingLead? lead)
    {
        var geoLocality = BuildGeoLocality(accessEvent.GeoCity, accessEvent.GeoRegionCode, accessEvent.GeoCountryCode);
        if (!string.IsNullOrWhiteSpace(geoLocality))
        {
            return geoLocality;
        }

        if (lead != null)
        {
            return BuildLeadLocality(lead);
        }

        return "Nao mapeado";
    }

    private static string BuildGeoLocality(string? city, string? regionCode, string? countryCode)
    {
        var cityPart = NormalizeFilter(city);
        var regionPart = NormalizeFilter(regionCode);
        var countryPart = NormalizeFilter(countryCode);

        if (cityPart != null && regionPart != null)
        {
            return $"{cityPart}/{regionPart.ToUpperInvariant()}";
        }

        if (cityPart != null && countryPart != null)
        {
            return $"{cityPart}/{countryPart.ToUpperInvariant()}";
        }

        return FirstNonEmpty(cityPart, regionPart, countryPart) ?? string.Empty;
    }

    private static string BuildLeadLocality(LandingLead lead)
    {
        var cityState = string.IsNullOrWhiteSpace(lead.City)
            ? null
            : string.IsNullOrWhiteSpace(lead.State)
                ? lead.City.Trim()
                : $"{lead.City.Trim()}/{lead.State.Trim().ToUpperInvariant()}";

        if (!string.IsNullOrWhiteSpace(lead.Neighborhood) && !string.IsNullOrWhiteSpace(cityState))
        {
            return $"{lead.Neighborhood.Trim()} - {cityState}";
        }

        return FirstNonEmpty(lead.Neighborhood, cityState) ?? "Nao informado";
    }

    private static bool HasGeo(LandingAccessEvent accessEvent)
        => !string.IsNullOrWhiteSpace(accessEvent.GeoCountryCode) ||
           !string.IsNullOrWhiteSpace(accessEvent.GeoRegionCode) ||
           !string.IsNullOrWhiteSpace(accessEvent.GeoCity);

    private static LandingLeadOrigin? NormalizeOriginFilter(string? rawOrigin)
    {
        if (string.IsNullOrWhiteSpace(rawOrigin))
        {
            return null;
        }

        return rawOrigin.Trim().ToLowerInvariant() switch
        {
            "client" => LandingLeadOrigin.Client,
            "provider" => LandingLeadOrigin.Provider,
            _ => null
        };
    }

    private static string ResolveOriginLabel(LandingLeadOrigin? origin)
    {
        return origin switch
        {
            LandingLeadOrigin.Client => "Cliente",
            LandingLeadOrigin.Provider => "Prestador",
            _ => "Origem nao informada"
        };
    }

    private static string ResolveEventLabel(LandingTelemetryEventType eventType)
    {
        return eventType switch
        {
            LandingTelemetryEventType.Heartbeat => "Heartbeat",
            LandingTelemetryEventType.ScrollMilestone => "Scroll milestone",
            LandingTelemetryEventType.Click => "Clique",
            LandingTelemetryEventType.LeadModalOpen => "Abertura de formulario",
            LandingTelemetryEventType.LeadSubmitSuccess => "Lead enviado",
            _ => "Evento"
        };
    }

    private static string? NormalizeFilter(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return rawValue.Trim();
    }

    private static bool ContainsAny(string? first, string? second, string needle)
    {
        return ContainsAny(new[] { first, second, needle });
    }

    private static bool ContainsAny(string? first, string? second, string? third, string needle)
    {
        return ContainsAny(new[] { first, second, third, needle });
    }

    private static bool ContainsAny(IEnumerable<string?> values, string needle)
    {
        return values.Any(value =>
            !string.IsNullOrWhiteSpace(value) &&
            value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsAny(params string?[] values)
    {
        if (values.Length == 0)
        {
            return false;
        }

        var needle = values[^1];
        if (string.IsNullOrWhiteSpace(needle))
        {
            return false;
        }

        return values.Take(values.Length - 1).Any(value =>
            !string.IsNullOrWhiteSpace(value) &&
            value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private sealed record LandingSessionAggregate(
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
        bool HasGeo,
        LandingAccessEvent AccessEvent,
        IReadOnlyList<LandingTelemetryEvent> TelemetryEvents,
        LandingLead? Lead);
}
