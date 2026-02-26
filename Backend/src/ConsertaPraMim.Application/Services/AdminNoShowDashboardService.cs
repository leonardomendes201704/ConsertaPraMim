using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ConsertaPraMim.Application.Services;

public class AdminNoShowDashboardService : IAdminNoShowDashboardService
{
    private const string FinancialPolicyAuditTargetType = "ServiceAppointmentFinancialPolicy";
    private const string FinancialPolicyAuditAction = "ServiceFinancialPolicyEventGenerated";
    private const int RecurrenceLookbackDays = 90;
    private const int RecurrenceTrendDays = 14;
    private const int RecurrenceTopActorsTake = 5;

    private readonly IAdminNoShowDashboardRepository _repository;
    private readonly IAdminAuditLogRepository _adminAuditLogRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMemoryCache? _memoryCache;

    public AdminNoShowDashboardService(
        IAdminNoShowDashboardRepository repository,
        IAdminAuditLogRepository? adminAuditLogRepository = null,
        IUserRepository? userRepository = null,
        IMemoryCache? memoryCache = null)
    {
        _repository = repository;
        _adminAuditLogRepository = adminAuditLogRepository ?? new NullAdminAuditLogRepository();
        _userRepository = userRepository ?? new NullUserRepository();
        _memoryCache = memoryCache;
    }

    public async Task<AdminNoShowDashboardDto> GetDashboardAsync(AdminNoShowDashboardQueryDto query)
    {
        var normalizedQuery = NormalizeQuery(query);
        var cacheKey = BuildDashboardCacheKey(normalizedQuery);

        if (_memoryCache == null)
        {
            return await BuildDashboardAsync(normalizedQuery);
        }

        return await _memoryCache.GetOrCreateAsync(
                cacheKey,
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);
                    return await BuildDashboardAsync(normalizedQuery);
                })
            ?? await BuildDashboardAsync(normalizedQuery);
    }

    public async Task<AdminKpiCardDto> GetKpiAsync(AdminNoShowDashboardQueryDto query, string kpiKey)
    {
        var dashboard = await GetDashboardAsync(query);
        return MapNoShowKpi(dashboard, kpiKey);
    }

    private async Task<AdminNoShowDashboardDto> BuildDashboardAsync(AdminNoShowDashboardQueryDto query)
    {
        var (fromUtc, toUtc) = NormalizeDateRange(query.FromUtc, query.ToUtc);
        var riskLevelFilter = ParseRiskLevel(query.RiskLevel);
        var queueTake = Math.Clamp(query.QueueTake, 1, 500);
        var cancellationNoShowWindowHours = Math.Clamp(query.CancellationNoShowWindowHours, 1, 168);

        var kpis = await _repository.GetKpisAsync(
            fromUtc,
            toUtc,
            query.City,
            query.Category,
            riskLevelFilter,
            cancellationNoShowWindowHours);

        var noShowByCategory = await _repository.GetBreakdownByCategoryAsync(
            fromUtc,
            toUtc,
            query.City,
            riskLevelFilter,
            cancellationNoShowWindowHours);

        var noShowByCity = await _repository.GetBreakdownByCityAsync(
            fromUtc,
            toUtc,
            query.Category,
            riskLevelFilter,
            cancellationNoShowWindowHours);

        var queue = await _repository.GetOpenRiskQueueAsync(
            fromUtc,
            toUtc,
            query.City,
            query.Category,
            riskLevelFilter,
            queueTake);

        var baseAppointments = kpis.BaseAppointments;
        var noShowRatePercent = CalculateRate(kpis.NoShowAppointments, baseAppointments);
        var attendanceRatePercent = CalculateRate(kpis.AttendanceAppointments, baseAppointments);
        var dualPresenceRatePercent = CalculateRate(kpis.DualPresenceConfirmedAppointments, baseAppointments);
        var highRiskConversionRatePercent = CalculateRate(kpis.HighRiskConvertedAppointments, kpis.HighRiskAppointments);
        var recurrenceSummary = await BuildRecurrenceSummaryAsync(toUtc);

        return new AdminNoShowDashboardDto(
            fromUtc,
            toUtc,
            query.City,
            query.Category,
            riskLevelFilter?.ToString(),
            baseAppointments,
            kpis.NoShowAppointments,
            noShowRatePercent,
            kpis.AttendanceAppointments,
            attendanceRatePercent,
            kpis.DualPresenceConfirmedAppointments,
            dualPresenceRatePercent,
            kpis.HighRiskAppointments,
            kpis.HighRiskConvertedAppointments,
            highRiskConversionRatePercent,
            kpis.OpenQueueItems,
            kpis.HighRiskOpenQueueItems,
            Math.Round(kpis.AverageQueueAgeMinutes, 1, MidpointRounding.AwayFromZero),
            noShowByCategory.Select(item => new AdminNoShowBreakdownDto(
                item.Name,
                item.BaseAppointments,
                item.NoShowAppointments,
                CalculateRate(item.NoShowAppointments, item.BaseAppointments),
                item.HighRiskAppointments)).ToList(),
            noShowByCity.Select(item => new AdminNoShowBreakdownDto(
                item.Name,
                item.BaseAppointments,
                item.NoShowAppointments,
                CalculateRate(item.NoShowAppointments, item.BaseAppointments),
                item.HighRiskAppointments)).ToList(),
            queue.Select(item => new AdminNoShowRiskQueueItemDto(
                item.QueueItemId,
                item.ServiceAppointmentId,
                item.ServiceRequestId,
                item.Category,
                item.City,
                item.ProviderName,
                item.ClientName,
                item.RiskLevel.ToString(),
                item.Score,
                item.ReasonsCsv,
                item.WindowStartUtc,
                item.LastDetectedAtUtc,
                item.FirstDetectedAtUtc)).ToList(),
            recurrenceSummary);
    }

    public async Task<string> ExportDashboardCsvAsync(AdminNoShowDashboardQueryDto query)
    {
        var dashboard = await GetDashboardAsync(query);
        return BuildCsv(dashboard);
    }

    private static (DateTime FromUtc, DateTime ToUtc) NormalizeDateRange(DateTime? fromUtc, DateTime? toUtc)
    {
        var nowUtc = DateTime.UtcNow;
        var normalizedTo = toUtc ?? nowUtc;
        var normalizedFrom = fromUtc ?? normalizedTo.AddDays(-30);

        if (normalizedFrom > normalizedTo)
        {
            (normalizedFrom, normalizedTo) = (normalizedTo, normalizedFrom);
        }

        return (normalizedFrom, normalizedTo);
    }

    private static ServiceAppointmentNoShowRiskLevel? ParseRiskLevel(string? riskLevel)
    {
        if (string.IsNullOrWhiteSpace(riskLevel))
        {
            return null;
        }

        return Enum.TryParse<ServiceAppointmentNoShowRiskLevel>(
            riskLevel.Trim(),
            ignoreCase: true,
            out var parsed)
            ? parsed
            : null;
    }

    private static AdminNoShowDashboardQueryDto NormalizeQuery(AdminNoShowDashboardQueryDto query)
    {
        return new AdminNoShowDashboardQueryDto(
            query.FromUtc?.ToUniversalTime(),
            query.ToUtc?.ToUniversalTime(),
            string.IsNullOrWhiteSpace(query.City) ? null : query.City.Trim(),
            string.IsNullOrWhiteSpace(query.Category) ? null : query.Category.Trim(),
            string.IsNullOrWhiteSpace(query.RiskLevel) ? null : query.RiskLevel.Trim(),
            Math.Clamp(query.QueueTake, 1, 500),
            Math.Clamp(query.CancellationNoShowWindowHours, 1, 168));
    }

    private static string BuildDashboardCacheKey(AdminNoShowDashboardQueryDto query)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"admin-no-show-dashboard:{query.FromUtc:o}:{query.ToUtc:o}:{query.City}:{query.Category}:{query.RiskLevel}:{query.QueueTake}:{query.CancellationNoShowWindowHours}");
    }

    private static decimal CalculateRate(int numerator, int denominator)
    {
        if (denominator <= 0)
        {
            return 0m;
        }

        var value = (decimal)numerator / denominator * 100m;
        return Math.Round(value, 1, MidpointRounding.AwayFromZero);
    }

    private static AdminKpiCardDto MapNoShowKpi(AdminNoShowDashboardDto dashboard, string kpiKey)
    {
        var generatedAtUtc = DateTime.UtcNow;
        var culture = CultureInfo.GetCultureInfo("pt-BR");
        var recurrence = dashboard.RecurrenceSummary;
        var normalizedKey = (kpiKey ?? string.Empty).Trim().ToLowerInvariant();

        return normalizedKey switch
        {
            "no-show-rate" => new AdminKpiCardDto(
                "no-show-rate",
                "Taxa de no-show",
                $"{dashboard.NoShowRatePercent.ToString("N1", culture)}%",
                null,
                new[]
                {
                    new AdminKpiDetailLineDto("No-show", dashboard.NoShowAppointments.ToString("N0", culture))
                },
                generatedAtUtc),
            "attendance-rate" => new AdminKpiCardDto(
                "attendance-rate",
                "Comparecimento",
                $"{dashboard.AttendanceRatePercent.ToString("N1", culture)}%",
                null,
                new[]
                {
                    new AdminKpiDetailLineDto("Atendidos", dashboard.AttendanceAppointments.ToString("N0", culture))
                },
                generatedAtUtc),
            "dual-confirmation-rate" => new AdminKpiCardDto(
                "dual-confirmation-rate",
                "Confirmacao dupla",
                $"{dashboard.DualPresenceConfirmationRatePercent.ToString("N1", culture)}%",
                null,
                new[]
                {
                    new AdminKpiDetailLineDto("Confirmados", dashboard.DualPresenceConfirmedAppointments.ToString("N0", culture))
                },
                generatedAtUtc),
            "high-risk" => new AdminKpiCardDto(
                "high-risk",
                "Risco alto",
                dashboard.HighRiskAppointments.ToString("N0", culture),
                null,
                new[]
                {
                    new AdminKpiDetailLineDto("Conversao", $"{dashboard.HighRiskConversionRatePercent.ToString("N1", culture)}%")
                },
                generatedAtUtc),
            "queue" => new AdminKpiCardDto(
                "queue",
                "Fila operacional",
                dashboard.OpenQueueItems.ToString("N0", culture),
                null,
                new[]
                {
                    new AdminKpiDetailLineDto("Idade media", $"{dashboard.AverageQueueAgeMinutes.ToString("N1", culture)} min")
                },
                generatedAtUtc),
            "client-recurrence" => new AdminKpiCardDto(
                "client-recurrence",
                "Reincidencia cliente (90d)",
                recurrence.ClientCriticalEvents.ToString("N0", culture),
                null,
                new[]
                {
                    new AdminKpiDetailLineDto("Usuarios reincidentes", $"{recurrence.RecurrentClients.ToString("N0", culture)} ({recurrence.ClientRecurrentRatePercent.ToString("N1", culture)}%)")
                },
                generatedAtUtc),
            "provider-recurrence" => new AdminKpiCardDto(
                "provider-recurrence",
                "Reincidencia prestador (90d)",
                recurrence.ProviderCriticalEvents.ToString("N0", culture),
                null,
                new[]
                {
                    new AdminKpiDetailLineDto("Usuarios reincidentes", $"{recurrence.RecurrentProviders.ToString("N0", culture)} ({recurrence.ProviderRecurrentRatePercent.ToString("N1", culture)}%)")
                },
                generatedAtUtc),
            "critical-clients" => new AdminKpiCardDto(
                "critical-clients",
                "Usuarios criticos (cliente)",
                recurrence.ClientsWithCriticalEvents.ToString("N0", culture),
                null,
                new[]
                {
                    new AdminKpiDetailLineDto("Com >= 2 eventos", recurrence.RecurrentClients.ToString("N0", culture))
                },
                generatedAtUtc),
            "critical-providers" => new AdminKpiCardDto(
                "critical-providers",
                "Usuarios criticos (prestador)",
                recurrence.ProvidersWithCriticalEvents.ToString("N0", culture),
                null,
                new[]
                {
                    new AdminKpiDetailLineDto("Com >= 2 eventos", recurrence.RecurrentProviders.ToString("N0", culture))
                },
                generatedAtUtc),
            _ => throw new KeyNotFoundException($"KPI de no-show '{kpiKey}' nao suportado.")
        };
    }

    private async Task<AdminNoShowRecurrenceSummaryDto> BuildRecurrenceSummaryAsync(DateTime dashboardToUtc)
    {
        var windowToUtc = NormalizeToUtc(dashboardToUtc);
        var windowFromUtc = windowToUtc.AddDays(-RecurrenceLookbackDays);

        var logs = await _adminAuditLogRepository.GetByTargetAndPeriodAsync(
            targetType: FinancialPolicyAuditTargetType,
            fromUtc: windowFromUtc,
            toUtc: windowToUtc,
            action: FinancialPolicyAuditAction,
            take: 10000);

        var events = logs
            .Select(ParseNoShowCriticalEvent)
            .Where(item => item != null)
            .Select(item => item!)
            .OrderBy(item => item.OccurredAtUtc)
            .ToList();

        var clientEvents = events
            .Where(item => string.Equals(item.ActorType, "Client", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var providerEvents = events
            .Where(item => string.Equals(item.ActorType, "Provider", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var clientGroups = BuildRecurrentActorAggregates(clientEvents, "Client");
        var providerGroups = BuildRecurrentActorAggregates(providerEvents, "Provider");

        var topClientGroups = clientGroups
            .OrderByDescending(item => item.CriticalEvents)
            .ThenByDescending(item => item.LastEventAtUtc)
            .Take(RecurrenceTopActorsTake)
            .ToList();
        var topProviderGroups = providerGroups
            .OrderByDescending(item => item.CriticalEvents)
            .ThenByDescending(item => item.LastEventAtUtc)
            .Take(RecurrenceTopActorsTake)
            .ToList();

        var actorNames = await ResolveActorNamesAsync(
            topClientGroups.Select(item => item.UserId)
                .Concat(topProviderGroups.Select(item => item.UserId)));

        var topClients = topClientGroups
            .Select(item => ToRecurrenceActorDto(item, actorNames))
            .ToList();
        var topProviders = topProviderGroups
            .Select(item => ToRecurrenceActorDto(item, actorNames))
            .ToList();

        var trendToDateUtc = windowToUtc.Date;
        var trendFromDateUtc = trendToDateUtc.AddDays(-(RecurrenceTrendDays - 1));
        var dailyTrend = BuildDailyTrend(events, trendFromDateUtc, trendToDateUtc);

        var recurrentClients = clientGroups.Count(item => item.CriticalEvents >= 2);
        var recurrentProviders = providerGroups.Count(item => item.CriticalEvents >= 2);

        return new AdminNoShowRecurrenceSummaryDto(
            WindowFromUtc: windowFromUtc,
            WindowToUtc: windowToUtc,
            LookbackDays: RecurrenceLookbackDays,
            ClientCriticalEvents: clientEvents.Count,
            ProviderCriticalEvents: providerEvents.Count,
            ClientsWithCriticalEvents: clientGroups.Count,
            ProvidersWithCriticalEvents: providerGroups.Count,
            RecurrentClients: recurrentClients,
            RecurrentProviders: recurrentProviders,
            ClientRecurrentRatePercent: CalculateRate(recurrentClients, clientGroups.Count),
            ProviderRecurrentRatePercent: CalculateRate(recurrentProviders, providerGroups.Count),
            TopRecurrentClients: topClients,
            TopRecurrentProviders: topProviders,
            DailyTrend: dailyTrend);
    }

    private async Task<Dictionary<Guid, string>> ResolveActorNamesAsync(IEnumerable<Guid> userIds)
    {
        var result = new Dictionary<Guid, string>();
        var uniqueIds = userIds
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToList();

        foreach (var userId in uniqueIds)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (!string.IsNullOrWhiteSpace(user?.Name))
            {
                result[userId] = user.Name.Trim();
            }
        }

        return result;
    }

    private static IReadOnlyList<RecurrentActorAggregate> BuildRecurrentActorAggregates(
        IReadOnlyList<NoShowCriticalEvent> events,
        string actorType)
    {
        return events
            .GroupBy(item => item.ActorUserId)
            .Select(group =>
            {
                var lastEvent = group
                    .OrderByDescending(item => item.OccurredAtUtc)
                    .ThenByDescending(item => item.EventType)
                    .First();

                return new RecurrentActorAggregate(
                    UserId: group.Key,
                    ActorType: actorType,
                    CriticalEvents: group.Count(),
                    LastEventAtUtc: lastEvent.OccurredAtUtc,
                    LastEventType: lastEvent.EventType,
                    LastOutcome: lastEvent.Outcome);
            })
            .ToList();
    }

    private static IReadOnlyList<AdminNoShowRecurrenceTrendPointDto> BuildDailyTrend(
        IReadOnlyList<NoShowCriticalEvent> events,
        DateTime trendFromDateUtc,
        DateTime trendToDateUtc)
    {
        var buckets = new Dictionary<DateTime, (int ClientEvents, int ProviderEvents)>();
        foreach (var item in events)
        {
            var dateUtc = item.OccurredAtUtc.Date;
            if (dateUtc < trendFromDateUtc || dateUtc > trendToDateUtc)
            {
                continue;
            }

            if (!buckets.TryGetValue(dateUtc, out var counters))
            {
                counters = (0, 0);
            }

            if (string.Equals(item.ActorType, "Client", StringComparison.OrdinalIgnoreCase))
            {
                counters = (counters.ClientEvents + 1, counters.ProviderEvents);
            }
            else
            {
                counters = (counters.ClientEvents, counters.ProviderEvents + 1);
            }

            buckets[dateUtc] = counters;
        }

        var trend = new List<AdminNoShowRecurrenceTrendPointDto>();
        for (var dateUtc = trendFromDateUtc; dateUtc <= trendToDateUtc; dateUtc = dateUtc.AddDays(1))
        {
            buckets.TryGetValue(dateUtc, out var counters);
            trend.Add(new AdminNoShowRecurrenceTrendPointDto(
                DateUtc: dateUtc,
                ClientCriticalEvents: counters.ClientEvents,
                ProviderCriticalEvents: counters.ProviderEvents,
                TotalCriticalEvents: counters.ClientEvents + counters.ProviderEvents));
        }

        return trend;
    }

    private static AdminNoShowRecurrenceActorDto ToRecurrenceActorDto(
        RecurrentActorAggregate aggregate,
        IReadOnlyDictionary<Guid, string> actorNames)
    {
        var actorName = actorNames.TryGetValue(aggregate.UserId, out var userName) &&
                        !string.IsNullOrWhiteSpace(userName)
            ? userName
            : $"{aggregate.ActorType} {aggregate.UserId.ToString("N")[..8]}";

        return new AdminNoShowRecurrenceActorDto(
            UserId: aggregate.UserId,
            UserName: actorName,
            CriticalEvents: aggregate.CriticalEvents,
            LastEventAtUtc: aggregate.LastEventAtUtc,
            LastEventType: MapFinancialEventTypeLabel(aggregate.LastEventType),
            LastOutcome: aggregate.LastOutcome);
    }

    private static NoShowCriticalEvent? ParseNoShowCriticalEvent(AdminAuditLog log)
    {
        if (string.IsNullOrWhiteSpace(log.Metadata))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(log.Metadata);
            var root = document.RootElement;
            var payload = TryGetProperty(root, "payload");
            if (!payload.HasValue || payload.Value.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var eventType = ReadString(payload.Value, "eventType");
            if (!TryResolveEventActor(eventType, root, out var actorType, out var actorUserId))
            {
                return null;
            }

            var outcome = ReadString(payload.Value, "outcome") ?? "unknown";
            var occurredAtUtc = ReadDateTime(payload.Value, "occurredAtUtc") ?? log.CreatedAt;

            return new NoShowCriticalEvent(
                ActorUserId: actorUserId,
                ActorType: actorType,
                EventType: eventType!,
                Outcome: outcome,
                OccurredAtUtc: NormalizeToUtc(occurredAtUtc));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryResolveEventActor(
        string? eventType,
        JsonElement root,
        out string actorType,
        out Guid actorUserId)
    {
        actorType = string.Empty;
        actorUserId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(eventType))
        {
            return false;
        }

        if (eventType.StartsWith("Client", StringComparison.OrdinalIgnoreCase))
        {
            actorType = "Client";
            actorUserId = ReadGuid(root, "clientId");
            return actorUserId != Guid.Empty;
        }

        if (eventType.StartsWith("Provider", StringComparison.OrdinalIgnoreCase))
        {
            actorType = "Provider";
            actorUserId = ReadGuid(root, "providerId");
            return actorUserId != Guid.Empty;
        }

        return false;
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static JsonElement? TryGetProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return element.TryGetProperty(propertyName, out var property)
            ? property
            : null;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        var property = TryGetProperty(element, propertyName);
        if (!property.HasValue)
        {
            return null;
        }

        return property.Value.ValueKind == JsonValueKind.String
            ? property.Value.GetString()
            : property.Value.ToString();
    }

    private static Guid ReadGuid(JsonElement element, string propertyName)
    {
        var raw = ReadString(element, propertyName);
        return Guid.TryParse(raw, out var parsed) ? parsed : Guid.Empty;
    }

    private static DateTime? ReadDateTime(JsonElement element, string propertyName)
    {
        var raw = ReadString(element, propertyName);
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string MapFinancialEventTypeLabel(string eventType)
    {
        return eventType switch
        {
            "ClientCancellation" => "Cancelamento tardio do cliente",
            "ClientNoShow" => "No-show do cliente",
            "ProviderCancellation" => "Cancelamento tardio do prestador",
            "ProviderNoShow" => "No-show do prestador",
            _ => eventType
        };
    }

    private static string BuildCsv(AdminNoShowDashboardDto dashboard)
    {
        var sb = new StringBuilder();
        AppendCsvRow(
            sb,
            "Section",
            "Name",
            "FromUtc",
            "ToUtc",
            "CityFilter",
            "CategoryFilter",
            "RiskLevelFilter",
            "BaseAppointments",
            "NoShowAppointments",
            "NoShowRatePercent",
            "AttendanceAppointments",
            "AttendanceRatePercent",
            "DualPresenceConfirmedAppointments",
            "DualPresenceConfirmationRatePercent",
            "HighRiskAppointments",
            "HighRiskConvertedAppointments",
            "HighRiskConversionRatePercent",
            "OpenQueueItems",
            "HighRiskOpenQueueItems",
            "AverageQueueAgeMinutes",
            "QueueItemId",
            "ServiceAppointmentId",
            "ServiceRequestId",
            "Category",
            "City",
            "ProviderName",
            "ClientName",
            "RiskLevel",
            "Score",
            "Reasons",
            "WindowStartUtc",
            "LastDetectedAtUtc",
            "FirstDetectedAtUtc");

        AppendCsvRow(
            sb,
            "Kpi",
            "Resumo",
            ToIso8601(dashboard.FromUtc),
            ToIso8601(dashboard.ToUtc),
            dashboard.CityFilter,
            dashboard.CategoryFilter,
            dashboard.RiskLevelFilter,
            ToInvariant(dashboard.BaseAppointments),
            ToInvariant(dashboard.NoShowAppointments),
            ToInvariant(dashboard.NoShowRatePercent),
            ToInvariant(dashboard.AttendanceAppointments),
            ToInvariant(dashboard.AttendanceRatePercent),
            ToInvariant(dashboard.DualPresenceConfirmedAppointments),
            ToInvariant(dashboard.DualPresenceConfirmationRatePercent),
            ToInvariant(dashboard.HighRiskAppointments),
            ToInvariant(dashboard.HighRiskConvertedAppointments),
            ToInvariant(dashboard.HighRiskConversionRatePercent),
            ToInvariant(dashboard.OpenQueueItems),
            ToInvariant(dashboard.HighRiskOpenQueueItems),
            ToInvariant(dashboard.AverageQueueAgeMinutes));

        foreach (var item in dashboard.NoShowByCategory)
        {
            AppendCsvRow(
                sb,
                "BreakdownCategory",
                item.Name,
                ToIso8601(dashboard.FromUtc),
                ToIso8601(dashboard.ToUtc),
                dashboard.CityFilter,
                dashboard.CategoryFilter,
                dashboard.RiskLevelFilter,
                ToInvariant(item.BaseAppointments),
                ToInvariant(item.NoShowAppointments),
                ToInvariant(item.NoShowRatePercent),
                null,
                null,
                null,
                null,
                ToInvariant(item.HighRiskAppointments));
        }

        foreach (var item in dashboard.NoShowByCity)
        {
            AppendCsvRow(
                sb,
                "BreakdownCity",
                item.Name,
                ToIso8601(dashboard.FromUtc),
                ToIso8601(dashboard.ToUtc),
                dashboard.CityFilter,
                dashboard.CategoryFilter,
                dashboard.RiskLevelFilter,
                ToInvariant(item.BaseAppointments),
                ToInvariant(item.NoShowAppointments),
                ToInvariant(item.NoShowRatePercent),
                null,
                null,
                null,
                null,
                ToInvariant(item.HighRiskAppointments));
        }

        foreach (var item in dashboard.OpenRiskQueue)
        {
            AppendCsvRow(
                sb,
                "OpenRiskQueue",
                $"{item.ProviderName} / {item.ClientName}",
                ToIso8601(dashboard.FromUtc),
                ToIso8601(dashboard.ToUtc),
                dashboard.CityFilter,
                dashboard.CategoryFilter,
                dashboard.RiskLevelFilter,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                item.QueueItemId.ToString(),
                item.ServiceAppointmentId.ToString(),
                item.ServiceRequestId.ToString(),
                item.Category,
                item.City,
                item.ProviderName,
                item.ClientName,
                item.RiskLevel,
                ToInvariant(item.Score),
                item.Reasons,
                ToIso8601(item.WindowStartUtc),
                ToIso8601(item.LastDetectedAtUtc),
                ToIso8601(item.FirstDetectedAtUtc));
        }

        return sb.ToString();
    }

    private static void AppendCsvRow(StringBuilder sb, params string?[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append(EscapeCsv(values[i]));
        }

        sb.AppendLine();
    }

    private static string ToIso8601(DateTime value)
    {
        return value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
    }

    private static string? ToInvariant<TValue>(TValue value)
    {
        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private sealed record NoShowCriticalEvent(
        Guid ActorUserId,
        string ActorType,
        string EventType,
        string Outcome,
        DateTime OccurredAtUtc);

    private sealed record RecurrentActorAggregate(
        Guid UserId,
        string ActorType,
        int CriticalEvents,
        DateTime LastEventAtUtc,
        string LastEventType,
        string LastOutcome);

    private sealed class NullAdminAuditLogRepository : IAdminAuditLogRepository
    {
        public Task AddAsync(AdminAuditLog auditLog)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AdminAuditLog>> GetByTargetAndPeriodAsync(
            string targetType,
            DateTime fromUtc,
            DateTime toUtc,
            Guid? actorUserId = null,
            Guid? targetId = null,
            string? action = null,
            int take = 2000)
        {
            return Task.FromResult<IReadOnlyList<AdminAuditLog>>(Array.Empty<AdminAuditLog>());
        }
    }

    private sealed class NullUserRepository : IUserRepository
    {
        public Task<User?> GetByEmailAsync(string email)
        {
            return Task.FromResult<User?>(null);
        }

        public Task<User> AddAsync(User user)
        {
            return Task.FromResult(user);
        }

        public Task<User?> GetByIdAsync(Guid id)
        {
            return Task.FromResult<User?>(null);
        }

        public Task UpdateAsync(User user)
        {
            return Task.CompletedTask;
        }

        public Task<IEnumerable<User>> GetAllAsync()
        {
            return Task.FromResult<IEnumerable<User>>(Array.Empty<User>());
        }
    }
}
