using System.Globalization;
using System.Text;
using System.Text.Json;
using AppMobileCPM.Services;
using Microsoft.Extensions.Options;

namespace AppMobileCPM.Integrations.Journey;

public sealed class JourneyProviderMatchingService : IJourneyProviderMatchingService
{
    private static readonly IReadOnlyDictionary<int, string[]> ProviderCategoryAliases = new Dictionary<int, string[]>
    {
        [1] = ["eletricista", "eletrica", "eletrico", "energia", "chuveiro", "tomada", "disjuntor", "iluminacao"],
        [2] = ["encanador", "hidraulica", "hidraulico", "vazamento", "torneira", "cano", "esgoto", "desentupimento", "caixa d agua"],
        [3] = ["eletronico", "televisao", "tv", "audio", "som", "celular", "tablet", "computador", "notebook", "impressora"],
        [4] = ["eletrodomestico", "geladeira", "freezer", "fogao", "microondas", "lava", "secadora", "ar condicionado", "purificador", "ventilador"],
        [5] = ["pedreiro", "alvenaria", "reforma", "pintura", "pintor", "telhado", "gesso", "serralheria", "montador", "moveis"],
        [6] = ["limpeza", "faxina", "diarista", "dedetizacao", "praga", "cupim", "barata", "formiga"],
        [99] = ["outros", "chaveiro"]
    };

    private static readonly IReadOnlyList<string> SubcategoryHints =
    [
        "chuveiro",
        "tomada",
        "disjuntor",
        "vazamento",
        "torneira",
        "cano",
        "desentupimento",
        "geladeira",
        "microondas",
        "maquina de lavar",
        "ar condicionado",
        "pintura",
        "telhado",
        "dedetizacao",
        "faxina",
        "montagem de moveis"
    ];

    private readonly IAdminKanbanService _kanbanService;
    private readonly JourneyProviderMatchingOptions _options;
    private readonly ILogger<JourneyProviderMatchingService> _logger;
    private readonly TimeZoneInfo _businessTimeZone;

    public JourneyProviderMatchingService(
        IAdminKanbanService kanbanService,
        IOptions<JourneyProviderMatchingOptions> options,
        ILogger<JourneyProviderMatchingService> logger)
    {
        _kanbanService = kanbanService;
        _options = options.Value;
        _logger = logger;
        _businessTimeZone = ResolveTimeZone(_options.Timezone);
    }

    public async Task<JourneyProviderMatchingRunResult> RunOnceAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        if (!_options.Enabled)
        {
            return new JourneyProviderMatchingRunResult();
        }

        var normalizedNowUtc = NormalizeUtc(nowUtc) ?? DateTime.UtcNow;
        var candidates = _kanbanService
            .ListJourneyStageAutomationCandidates(AdminKanbanBoardTypes.Clients, normalizedNowUtc, _options.WorkerBatchSize)
            .Where(item => string.Equals(item.CurrentState, AdminKanbanJourneyStates.AppointmentConfirmed, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var updatedCount = 0;
        var eligibleJourneysCount = 0;
        var noCoverageJourneysCount = 0;

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var journey = _kanbanService.GetJourneyDetails(candidate.LeadId);
            if (journey is null || !string.Equals(journey.BoardType, AdminKanbanBoardTypes.Clients, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var requestedCategory = FirstNonEmpty(journey.Qualification.NormalizedServiceCategoryName);
            var requestedSubcategory = ResolveRequestedSubcategory(journey.Qualification.ProblemContext);
            var scheduleStartAtUtc = NormalizeUtc(journey.Scheduling.ScheduledStartAtUtc);
            var scheduleEndAtUtc = NormalizeUtc(journey.Scheduling.ScheduledEndAtUtc);
            var latitude = journey.Qualification.Latitude;
            var longitude = journey.Qualification.Longitude;

            if (string.IsNullOrWhiteSpace(requestedCategory) ||
                !latitude.HasValue ||
                !longitude.HasValue ||
                !scheduleStartAtUtc.HasValue ||
                !scheduleEndAtUtc.HasValue)
            {
                PersistMatchingSnapshot(
                    journey,
                    normalizedNowUtc,
                    AdminKanbanJourneyMatchingStatuses.NoCoverage,
                    "Matching nao executado porque a jornada nao possui categoria, coordenadas ou janela confirmada suficientes.",
                    requestedCategory,
                    requestedSubcategory,
                    [],
                    0,
                    0);

                _kanbanService.ApplyJourneyStageAutomation(new AdminKanbanJourneyStageAutomationUpdateRequest
                {
                    LeadId = journey.LeadId,
                    BoardType = journey.BoardType,
                    TargetStageName = AdminKanbanJourneyClientStageNames.OperationalException,
                    TargetCurrentState = AdminKanbanJourneyStates.OperationalException,
                    Reason = "Matching nao conseguiu rodar por falta de dados minimos da jornada.",
                    Origin = AdminKanbanJourneyAutomationOrigins.MatchingEngine,
                    HistoryEventType = "jornada_matching_dados_insuficientes",
                    HistoryDescription = "A jornada foi movida para excecao operacional porque o matching nao encontrou categoria, coordenadas ou janela confirmada suficientes.",
                    MetadataJson = BuildMatchingMetadataJson(journey, requestedCategory, requestedSubcategory, [])
                });

                updatedCount++;
                noCoverageJourneysCount++;
                continue;
            }

            var providers = _kanbanService.ListJourneyProviderProfiles(scheduleStartAtUtc, scheduleEndAtUtc);
            var evaluated = providers
                .Select(provider => EvaluateProvider(
                    provider,
                    requestedCategory,
                    requestedSubcategory,
                    latitude.Value,
                    longitude.Value,
                    scheduleStartAtUtc.Value,
                    scheduleEndAtUtc.Value))
                .OrderByDescending(item => item.IsEligible)
                .ThenByDescending(item => item.Score)
                .ThenBy(item => item.DistanceKm)
                .ToList();
            var ranked = evaluated
                .Select((item, index) => new AdminKanbanJourneyProviderMatchRecord
                {
                    ProviderId = item.ProviderId,
                    ProviderName = item.ProviderName,
                    ProviderEmail = item.ProviderEmail,
                    ProviderPhone = item.ProviderPhone,
                    IsEligible = item.IsEligible,
                    RankPosition = item.IsEligible ? index + 1 : 0,
                    Score = item.Score,
                    DistanceKm = item.DistanceKm,
                    CoverageRadiusKm = item.CoverageRadiusKm,
                    Rating = item.Rating,
                    ReviewCount = item.ReviewCount,
                    OperationalStatus = item.OperationalStatus,
                    ClientPreference = item.ClientPreference,
                    RequestedCategory = item.RequestedCategory,
                    RequestedSubcategory = item.RequestedSubcategory,
                    CategoryMatched = item.CategoryMatched,
                    SubcategoryMatched = item.SubcategoryMatched,
                    RadiusMatched = item.RadiusMatched,
                    AvailabilityMatched = item.AvailabilityMatched,
                    CapacityMatched = item.CapacityMatched,
                    BlockReasonCode = item.BlockReasonCode,
                    BlockReasonLabel = item.BlockReasonLabel,
                    Summary = item.Summary
                })
                .ToList();
            var persistedCandidates = ranked
                .Take(Math.Max(1, _options.MaxCandidatesToPersist))
                .ToList();

            var eligible = ranked.Where(item => item.IsEligible).ToList();
            var matchingStatus = eligible.Count > 0
                ? AdminKanbanJourneyMatchingStatuses.EligibleProvidersFound
                : AdminKanbanJourneyMatchingStatuses.NoCoverage;
            var matchingSummary = eligible.Count > 0
                ? $"Matching geografico encontrou {eligible.Count} prestador(es) elegivel(is) para a jornada."
                : "Nenhum prestador elegivel foi encontrado para a categoria e raio informados.";

            PersistMatchingSnapshot(
                journey,
                normalizedNowUtc,
                matchingStatus,
                matchingSummary,
                requestedCategory,
                requestedSubcategory,
                persistedCandidates,
                evaluated.Count,
                eligible.Count);

            var automationApplied = _kanbanService.ApplyJourneyStageAutomation(new AdminKanbanJourneyStageAutomationUpdateRequest
            {
                LeadId = journey.LeadId,
                BoardType = journey.BoardType,
                TargetStageName = eligible.Count > 0
                    ? AdminKanbanJourneyClientStageNames.MatchingInProgress
                    : AdminKanbanJourneyClientStageNames.NoMatch,
                TargetCurrentState = eligible.Count > 0
                    ? AdminKanbanJourneyStates.MatchingInProgress
                    : AdminKanbanJourneyStates.NoMatch,
                Reason = eligible.Count > 0
                    ? $"Matching geografico concluiu a triagem de {eligible.Count} prestador(es) elegivel(is)."
                    : "Matching geografico nao encontrou cobertura suficiente para o atendimento.",
                Origin = AdminKanbanJourneyAutomationOrigins.MatchingEngine,
                HistoryEventType = eligible.Count > 0
                    ? "jornada_matching_concluido"
                    : "jornada_matching_sem_cobertura",
                HistoryDescription = eligible.Count > 0
                    ? "A jornada avancou para o matching geografico apos confirmar a agenda, com prestadores elegiveis ranqueados."
                    : "A jornada foi marcada como sem match porque nenhum prestador elegivel foi encontrado dentro do recorte atual.",
                MetadataJson = BuildMatchingMetadataJson(journey, requestedCategory, requestedSubcategory, persistedCandidates)
            });

            if (automationApplied is null)
            {
                continue;
            }

            updatedCount++;
            if (eligible.Count > 0)
            {
                eligibleJourneysCount++;
            }
            else
            {
                noCoverageJourneysCount++;
            }
        }

        _logger.LogInformation(
            "JourneyProviderMatchingService processou {ScannedCount} jornada(s). Updated={UpdatedCount} Eligible={EligibleJourneysCount} NoCoverage={NoCoverageJourneysCount}.",
            candidates.Count,
            updatedCount,
            eligibleJourneysCount,
            noCoverageJourneysCount);

        return new JourneyProviderMatchingRunResult
        {
            ScannedCount = candidates.Count,
            UpdatedCount = updatedCount,
            EligibleJourneysCount = eligibleJourneysCount,
            NoCoverageJourneysCount = noCoverageJourneysCount
        };
    }

    private void PersistMatchingSnapshot(
        AdminKanbanLeadJourneyRecord journey,
        DateTime nowUtc,
        string status,
        string summary,
        string requestedCategory,
        string requestedSubcategory,
        IReadOnlyList<AdminKanbanJourneyProviderMatchRecord> candidates,
        int evaluatedProvidersCount,
        int eligibleProvidersCount)
    {
        _kanbanService.UpdateJourneyMatching(
            journey.LeadId,
            new AdminKanbanJourneyMatchingUpdateRequest
            {
                Status = status,
                Summary = summary,
                RequestedCategory = requestedCategory,
                RequestedSubcategory = requestedSubcategory,
                EvaluatedProvidersCount = Math.Max(evaluatedProvidersCount, candidates.Count),
                EligibleProvidersCount = Math.Max(eligibleProvidersCount, candidates.Count(item => item.IsEligible)),
                LastRunAtUtc = nowUtc,
                CurrentState = journey.CurrentState,
                HistoryEventType = "jornada_matching_snapshot",
                HistoryDescription = summary,
                SourceChannel = journey.SourceChannel,
                MetadataJson = BuildMatchingMetadataJson(journey, requestedCategory, requestedSubcategory, candidates),
                Candidates = candidates
            });
    }

    private AdminKanbanJourneyProviderMatchRecord EvaluateProvider(
        AdminKanbanJourneyProviderProfileRecord provider,
        string requestedCategory,
        string requestedSubcategory,
        double clientLatitude,
        double clientLongitude,
        DateTime scheduledStartAtUtc,
        DateTime scheduledEndAtUtc)
    {
        var categoryMatched = MatchesCategory(provider.CategoryCodes, requestedCategory);
        var subcategoryMatched = MatchesSubcategory(provider.SpecialtyHints, requestedSubcategory);
        var radiusMatched = provider.BaseLatitude.HasValue &&
            provider.BaseLongitude.HasValue &&
            provider.RadiusKm > 0 &&
            CalculateDistanceKm(provider.BaseLatitude.Value, provider.BaseLongitude.Value, clientLatitude, clientLongitude) <= provider.RadiusKm;
        var availabilityMatched = MatchesAvailability(provider.AvailabilityRules, scheduledStartAtUtc, scheduledEndAtUtc);
        var statusMatched = provider.IsActive &&
            provider.IsOnboardingCompleted &&
            provider.OnboardingStatusCode == 2 &&
            !provider.HasOperationalCompliancePending &&
            provider.TrustStatusCode != 3 &&
            provider.OperationalStatusCode is 1 or 2;
        var capacityMatched = provider.ConflictingAppointmentsCount <= 0;

        var distanceKm = provider.BaseLatitude.HasValue && provider.BaseLongitude.HasValue
            ? CalculateDistanceKm(provider.BaseLatitude.Value, provider.BaseLongitude.Value, clientLatitude, clientLongitude)
            : double.MaxValue;
        var isEligible = categoryMatched && subcategoryMatched && radiusMatched && availabilityMatched && statusMatched && capacityMatched;
        var blockReasonCode = ResolveBlockReasonCode(categoryMatched, subcategoryMatched, radiusMatched, availabilityMatched, statusMatched, capacityMatched, provider);
        var blockReasonLabel = ResolveBlockReasonLabel(blockReasonCode);
        var score = isEligible
            ? CalculateScore(provider, distanceKm, requestedSubcategory, subcategoryMatched)
            : 0m;

        return new AdminKanbanJourneyProviderMatchRecord
        {
            ProviderId = provider.ProviderId,
            ProviderName = provider.ProviderName,
            ProviderEmail = provider.ProviderEmail,
            ProviderPhone = provider.ProviderPhone,
            IsEligible = isEligible,
            RankPosition = 0,
            Score = score,
            DistanceKm = double.IsFinite(distanceKm) ? distanceKm : 0d,
            CoverageRadiusKm = provider.RadiusKm,
            Rating = provider.Rating,
            ReviewCount = provider.ReviewCount,
            OperationalStatus = ResolveOperationalStatusLabel(provider.OperationalStatusCode),
            ClientPreference = ResolveClientPreferenceLabel(provider.ClientPreferenceCode),
            RequestedCategory = requestedCategory,
            RequestedSubcategory = requestedSubcategory,
            CategoryMatched = categoryMatched,
            SubcategoryMatched = subcategoryMatched,
            RadiusMatched = radiusMatched,
            AvailabilityMatched = availabilityMatched,
            CapacityMatched = capacityMatched,
            BlockReasonCode = blockReasonCode,
            BlockReasonLabel = blockReasonLabel,
            Summary = isEligible
                ? "Prestador elegivel para seguir para a proxima etapa da jornada."
                : $"Prestador bloqueado no matching: {blockReasonLabel}."
        };
    }

    private static decimal CalculateScore(
        AdminKanbanJourneyProviderProfileRecord provider,
        double distanceKm,
        string requestedSubcategory,
        bool subcategoryMatched)
    {
        var score = 0m;

        score += provider.OperationalStatusCode == 1 ? 30m : 20m;
        score += Math.Max(0m, 25m - (decimal)Math.Min(distanceKm, 25d));
        score += (decimal)Math.Min(provider.Rating, 5d) * 8m;
        score += Math.Min(provider.ReviewCount, 50) * 0.3m;
        score += provider.TrustStatusCode == 2 ? 8m : 0m;
        score += !string.IsNullOrWhiteSpace(requestedSubcategory) && subcategoryMatched ? 6m : 0m;

        return Math.Round(score, 2, MidpointRounding.AwayFromZero);
    }

    private bool MatchesAvailability(
        IReadOnlyList<AdminKanbanJourneyProviderAvailabilityRuleRecord> rules,
        DateTime scheduledStartAtUtc,
        DateTime scheduledEndAtUtc)
    {
        if (rules.Count == 0)
        {
            return true;
        }

        var startLocal = TimeZoneInfo.ConvertTimeFromUtc(scheduledStartAtUtc, _businessTimeZone);
        var endLocal = TimeZoneInfo.ConvertTimeFromUtc(scheduledEndAtUtc, _businessTimeZone);
        var dayCode = (int)startLocal.DayOfWeek;

        return rules.Any(rule =>
            rule.DayOfWeekCode == dayCode &&
            rule.StartTime <= startLocal.TimeOfDay &&
            rule.EndTime >= endLocal.TimeOfDay);
    }

    private static bool MatchesCategory(IReadOnlyList<int> categoryCodes, string requestedCategory)
    {
        if (categoryCodes.Count == 0 || string.IsNullOrWhiteSpace(requestedCategory))
        {
            return false;
        }

        var normalizedRequestedCategory = NormalizeToken(requestedCategory);
        return categoryCodes.Any(code =>
            ProviderCategoryAliases.TryGetValue(code, out var aliases) &&
            aliases.Any(alias => normalizedRequestedCategory.Contains(NormalizeToken(alias), StringComparison.Ordinal)));
    }

    private static bool MatchesSubcategory(string specialtyHints, string requestedSubcategory)
    {
        if (string.IsNullOrWhiteSpace(requestedSubcategory))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(specialtyHints))
        {
            return true;
        }

        return NormalizeToken(specialtyHints).Contains(NormalizeToken(requestedSubcategory), StringComparison.Ordinal);
    }

    private static string ResolveRequestedSubcategory(string problemContext)
    {
        var normalized = NormalizeToken(problemContext);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return SubcategoryHints.FirstOrDefault(hint => normalized.Contains(NormalizeToken(hint), StringComparison.Ordinal)) ?? string.Empty;
    }

    private static string ResolveBlockReasonCode(
        bool categoryMatched,
        bool subcategoryMatched,
        bool radiusMatched,
        bool availabilityMatched,
        bool statusMatched,
        bool capacityMatched,
        AdminKanbanJourneyProviderProfileRecord provider)
    {
        if (!provider.IsActive)
        {
            return "provider_inactive";
        }

        if (!provider.IsOnboardingCompleted || provider.OnboardingStatusCode != 2)
        {
            return "provider_onboarding_pending";
        }

        if (provider.HasOperationalCompliancePending)
        {
            return "provider_compliance_pending";
        }

        if (provider.TrustStatusCode == 3)
        {
            return "provider_restricted";
        }

        if (!categoryMatched)
        {
            return "category_mismatch";
        }

        if (!subcategoryMatched)
        {
            return "subcategory_mismatch";
        }

        if (!radiusMatched)
        {
            return "outside_radius";
        }

        if (!availabilityMatched)
        {
            return "outside_availability";
        }

        if (!statusMatched)
        {
            return "operational_unavailable";
        }

        if (!capacityMatched)
        {
            return "appointment_conflict";
        }

        return string.Empty;
    }

    private static string ResolveBlockReasonLabel(string code) => code switch
    {
        "provider_inactive" => "Prestador inativo",
        "provider_onboarding_pending" => "Prestador com onboarding pendente",
        "provider_compliance_pending" => "Prestador com pendencia operacional",
        "provider_restricted" => "Prestador com restricao operacional",
        "category_mismatch" => "Categoria fora do escopo",
        "subcategory_mismatch" => "Subcategoria nao aderente",
        "outside_radius" => "Fora do raio de atendimento",
        "outside_availability" => "Fora da disponibilidade informada",
        "operational_unavailable" => "Status operacional indisponivel",
        "appointment_conflict" => "Janela conflita com outro atendimento",
        _ => "-"
    };

    private static string ResolveOperationalStatusLabel(int code) => code switch
    {
        1 => "Online",
        2 => "Em atendimento",
        _ => "Ausente"
    };

    private static string ResolveClientPreferenceLabel(int code) => code switch
    {
        1 => "Somente PF",
        2 => "Somente PJ",
        _ => "PF e PJ"
    };

    private static double CalculateDistanceKm(double fromLat, double fromLng, double toLat, double toLng)
    {
        const double earthRadiusKm = 6371.0;
        var dLat = DegreesToRadians(toLat - fromLat);
        var dLng = DegreesToRadians(toLng - fromLng);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(fromLat)) * Math.Cos(DegreesToRadians(toLat)) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static string BuildMatchingMetadataJson(
        AdminKanbanLeadJourneyRecord journey,
        string requestedCategory,
        string requestedSubcategory,
        IReadOnlyList<AdminKanbanJourneyProviderMatchRecord> candidates)
    {
        var payload = new
        {
            journeyId = journey.JourneyId,
            leadId = journey.LeadId,
            requestedCategory,
            requestedSubcategory,
            scheduledStartAtUtc = journey.Scheduling.ScheduledStartAtUtc,
            scheduledEndAtUtc = journey.Scheduling.ScheduledEndAtUtc,
            candidates = candidates.Select(item => new
            {
                providerId = item.ProviderId,
                providerName = item.ProviderName,
                isEligible = item.IsEligible,
                rankPosition = item.RankPosition,
                score = item.Score,
                distanceKm = item.DistanceKm,
                blockReasonCode = item.BlockReasonCode,
                blockReasonLabel = item.BlockReasonLabel
            })
        };

        return JsonSerializer.Serialize(payload);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value
            .Trim()
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);

        Span<char> buffer = stackalloc char[normalized.Length];
        var index = 0;
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            buffer[index++] = char.IsLetterOrDigit(character) ? character : ' ';
        }

        return new string(buffer[..index]).Trim();
    }

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    private static TimeZoneInfo ResolveTimeZone(string timezone)
    {
        if (!string.IsNullOrWhiteSpace(timezone))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timezone);
            }
            catch (TimeZoneNotFoundException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}
