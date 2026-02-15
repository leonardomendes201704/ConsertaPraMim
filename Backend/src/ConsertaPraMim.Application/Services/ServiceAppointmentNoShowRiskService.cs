using System.Text.Json;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ConsertaPraMim.Application.Services;

public class ServiceAppointmentNoShowRiskService : IServiceAppointmentNoShowRiskService
{
    private readonly IServiceAppointmentRepository _serviceAppointmentRepository;
    private readonly IServiceAppointmentNoShowRiskPolicyRepository _policyRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ServiceAppointmentNoShowRiskService> _logger;
    private readonly int _lookaheadHours;
    private readonly int _includePastWindowMinutes;

    public ServiceAppointmentNoShowRiskService(
        IServiceAppointmentRepository serviceAppointmentRepository,
        IServiceAppointmentNoShowRiskPolicyRepository policyRepository,
        INotificationService notificationService,
        IConfiguration configuration,
        ILogger<ServiceAppointmentNoShowRiskService> logger)
    {
        _serviceAppointmentRepository = serviceAppointmentRepository;
        _policyRepository = policyRepository;
        _notificationService = notificationService;
        _logger = logger;
        _lookaheadHours = ParseInt(configuration["ServiceAppointments:NoShowRisk:LookaheadHours"], 24, 1, 168);
        _includePastWindowMinutes = ParseInt(configuration["ServiceAppointments:NoShowRisk:IncludePastWindowMinutes"], 30, 0, 240);
    }

    public async Task<int> EvaluateNoShowRiskAsync(int batchSize = 200, CancellationToken cancellationToken = default)
    {
        var policy = await _policyRepository.GetActiveAsync();
        if (policy == null)
        {
            _logger.LogWarning("No-show risk evaluation skipped because there is no active policy.");
            return 0;
        }

        var nowUtc = DateTime.UtcNow;
        var fromUtc = nowUtc.AddMinutes(-_includePastWindowMinutes);
        var toUtc = nowUtc.AddHours(_lookaheadHours);
        var candidates = await _serviceAppointmentRepository.GetNoShowRiskCandidatesAsync(fromUtc, toUtc, batchSize);
        if (candidates.Count == 0)
        {
            return 0;
        }

        var clientHistoryCache = new Dictionary<Guid, int>();
        var providerHistoryCache = new Dictionary<Guid, int>();
        var processed = 0;

        foreach (var appointment in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var previousScore = appointment.NoShowRiskScore;
            var previousLevel = appointment.NoShowRiskLevel;
            var previousReasons = appointment.NoShowRiskReasons;

            var assessment = await CalculateRiskAsync(
                appointment,
                policy,
                nowUtc,
                clientHistoryCache,
                providerHistoryCache);

            appointment.NoShowRiskScore = assessment.Score;
            appointment.NoShowRiskLevel = assessment.Level;
            appointment.NoShowRiskReasons = assessment.ReasonsCsv;
            appointment.NoShowRiskCalculatedAtUtc = nowUtc;

            await _serviceAppointmentRepository.UpdateAsync(appointment);

            var hasChanged =
                previousScore != assessment.Score ||
                previousLevel != assessment.Level ||
                !string.Equals(previousReasons, assessment.ReasonsCsv, StringComparison.Ordinal);

            if (hasChanged)
            {
                await _serviceAppointmentRepository.AddHistoryAsync(new ServiceAppointmentHistory
                {
                    ServiceAppointmentId = appointment.Id,
                    PreviousStatus = appointment.Status,
                    NewStatus = appointment.Status,
                    ActorRole = ServiceAppointmentActorRole.System,
                    Reason = $"Risco no-show recalculado: {assessment.Level} ({assessment.Score}).",
                    Metadata = BuildHistoryMetadataJson(previousScore, previousLevel, previousReasons, assessment),
                    OccurredAtUtc = nowUtc
                });

                if (assessment.Level is ServiceAppointmentNoShowRiskLevel.Medium or ServiceAppointmentNoShowRiskLevel.High &&
                    previousLevel != assessment.Level)
                {
                    await NotifyRiskChangedAsync(appointment, assessment);
                }
            }

            processed++;
        }

        return processed;
    }

    private async Task NotifyRiskChangedAsync(ServiceAppointment appointment, RiskAssessmentResult assessment)
    {
        var riskLabel = assessment.Level == ServiceAppointmentNoShowRiskLevel.High ? "alto" : "medio";
        var subject = $"Alerta preventivo: risco {riskLabel} de no-show";
        var message = BuildPreventiveNotificationMessage(appointment, assessment);
        var actionUrl = $"/ServiceRequests/Details/{appointment.ServiceRequestId}?appointmentId={appointment.Id}";

        try
        {
            await _notificationService.SendNotificationAsync(
                appointment.ClientId.ToString("N"),
                subject,
                message,
                actionUrl);

            await _notificationService.SendNotificationAsync(
                appointment.ProviderId.ToString("N"),
                subject,
                message,
                actionUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send preventive no-show risk notification for appointment {AppointmentId}.", appointment.Id);
        }
    }

    private async Task<RiskAssessmentResult> CalculateRiskAsync(
        ServiceAppointment appointment,
        ServiceAppointmentNoShowRiskPolicy policy,
        DateTime nowUtc,
        IDictionary<Guid, int> clientHistoryCache,
        IDictionary<Guid, int> providerHistoryCache)
    {
        var score = 0;
        var reasons = new List<string>();

        var clientConfirmed = appointment.ClientPresenceConfirmed == true;
        var providerConfirmed = appointment.ProviderPresenceConfirmed == true;
        if (!clientConfirmed)
        {
            score += policy.WeightClientNotConfirmed;
            reasons.Add("client_presence_not_confirmed");
        }

        if (!providerConfirmed)
        {
            score += policy.WeightProviderNotConfirmed;
            reasons.Add("provider_presence_not_confirmed");
        }

        if (!clientConfirmed && !providerConfirmed)
        {
            score += policy.WeightBothNotConfirmedBonus;
            reasons.Add("both_presence_not_confirmed");
        }

        var hoursToWindow = (appointment.WindowStartUtc - nowUtc).TotalHours;
        if (hoursToWindow <= 2)
        {
            score += policy.WeightWindowWithin2Hours;
            reasons.Add("window_within_2h");
        }
        else if (hoursToWindow <= 6)
        {
            score += policy.WeightWindowWithin6Hours;
            reasons.Add("window_within_6h");
        }
        else if (hoursToWindow <= 24)
        {
            score += policy.WeightWindowWithin24Hours;
            reasons.Add("window_within_24h");
        }

        var historyFromUtc = nowUtc.AddDays(-policy.LookbackDays);
        var clientRiskEvents = await GetClientRiskEventsAsync(appointment.ClientId, historyFromUtc, nowUtc, clientHistoryCache);
        if (clientRiskEvents >= policy.MinClientHistoryRiskEvents)
        {
            score += policy.WeightClientHistoryRisk;
            reasons.Add("client_history_risk");
        }

        var providerRiskEvents = await GetProviderRiskEventsAsync(appointment.ProviderId, historyFromUtc, nowUtc, providerHistoryCache);
        if (providerRiskEvents >= policy.MinProviderHistoryRiskEvents)
        {
            score += policy.WeightProviderHistoryRisk;
            reasons.Add("provider_history_risk");
        }

        var clampedScore = Math.Clamp(score, 0, 100);
        var level = ResolveRiskLevel(policy, clampedScore);
        var reasonsCsv = string.Join(",", reasons.Distinct(StringComparer.Ordinal));
        return new RiskAssessmentResult(clampedScore, level, reasonsCsv);
    }

    private async Task<int> GetClientRiskEventsAsync(
        Guid clientId,
        DateTime fromUtc,
        DateTime toUtc,
        IDictionary<Guid, int> cache)
    {
        if (cache.TryGetValue(clientId, out var cached))
        {
            return cached;
        }

        var count = await _serviceAppointmentRepository.CountClientNoShowRiskEventsAsync(clientId, fromUtc, toUtc);
        cache[clientId] = count;
        return count;
    }

    private async Task<int> GetProviderRiskEventsAsync(
        Guid providerId,
        DateTime fromUtc,
        DateTime toUtc,
        IDictionary<Guid, int> cache)
    {
        if (cache.TryGetValue(providerId, out var cached))
        {
            return cached;
        }

        var count = await _serviceAppointmentRepository.CountProviderNoShowRiskEventsAsync(providerId, fromUtc, toUtc);
        cache[providerId] = count;
        return count;
    }

    private static ServiceAppointmentNoShowRiskLevel ResolveRiskLevel(ServiceAppointmentNoShowRiskPolicy policy, int score)
    {
        if (score >= policy.HighThresholdScore)
        {
            return ServiceAppointmentNoShowRiskLevel.High;
        }

        if (score >= policy.MediumThresholdScore)
        {
            return ServiceAppointmentNoShowRiskLevel.Medium;
        }

        return ServiceAppointmentNoShowRiskLevel.Low;
    }

    private static string BuildHistoryMetadataJson(
        int? previousScore,
        ServiceAppointmentNoShowRiskLevel? previousLevel,
        string? previousReasons,
        RiskAssessmentResult assessment)
    {
        var metadata = new
        {
            type = "no_show_risk_assessment",
            previous = new
            {
                score = previousScore,
                level = previousLevel?.ToString(),
                reasons = previousReasons
            },
            current = new
            {
                score = assessment.Score,
                level = assessment.Level.ToString(),
                reasons = assessment.ReasonsCsv
            }
        };

        return JsonSerializer.Serialize(metadata);
    }

    private static string BuildPreventiveNotificationMessage(ServiceAppointment appointment, RiskAssessmentResult assessment)
    {
        var reasons = assessment.ReasonsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(MapReasonToPtBr)
            .ToArray();

        var reasonsText = reasons.Length == 0
            ? "Sem motivos detalhados."
            : string.Join("; ", reasons);

        return $"Agendamento em {appointment.WindowStartUtc:dd/MM HH:mm} com score {assessment.Score}/100. Motivos: {reasonsText}";
    }

    private static string MapReasonToPtBr(string reasonCode)
    {
        return reasonCode switch
        {
            "client_presence_not_confirmed" => "cliente ainda nao confirmou presenca",
            "provider_presence_not_confirmed" => "prestador ainda nao confirmou presenca",
            "both_presence_not_confirmed" => "nenhuma das partes confirmou presenca",
            "window_within_24h" => "visita ocorre em ate 24h",
            "window_within_6h" => "visita ocorre em ate 6h",
            "window_within_2h" => "visita ocorre em ate 2h",
            "client_history_risk" => "historico recente de cancelamentos do cliente",
            "provider_history_risk" => "historico recente de cancelamentos/expiracoes do prestador",
            _ => reasonCode
        };
    }

    private static int ParseInt(string? raw, int defaultValue, int min, int max)
    {
        if (!int.TryParse(raw, out var parsed))
        {
            return defaultValue;
        }

        return Math.Clamp(parsed, min, max);
    }

    private sealed record RiskAssessmentResult(
        int Score,
        ServiceAppointmentNoShowRiskLevel Level,
        string ReasonsCsv);
}
