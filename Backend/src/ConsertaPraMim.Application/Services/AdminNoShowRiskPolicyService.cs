using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConsertaPraMim.Application.Services;

public class AdminNoShowRiskPolicyService : IAdminNoShowRiskPolicyService
{
    private readonly IServiceAppointmentNoShowRiskPolicyRepository _policyRepository;
    private readonly IAdminAuditLogRepository _adminAuditLogRepository;
    private readonly ILogger<AdminNoShowRiskPolicyService> _logger;

    public AdminNoShowRiskPolicyService(
        IServiceAppointmentNoShowRiskPolicyRepository policyRepository,
        IAdminAuditLogRepository adminAuditLogRepository,
        ILogger<AdminNoShowRiskPolicyService>? logger = null)
    {
        _policyRepository = policyRepository;
        _adminAuditLogRepository = adminAuditLogRepository;
        _logger = logger ?? NullLogger<AdminNoShowRiskPolicyService>.Instance;
    }

    public async Task<AdminNoShowRiskPolicyDto?> GetActiveAsync()
    {
        var policy = await _policyRepository.GetActiveAsync();
        return policy == null ? null : MapDto(policy);
    }

    public async Task<AdminNoShowRiskPolicyUpdateResultDto> UpdateActiveAsync(
        AdminUpdateNoShowRiskPolicyRequestDto request,
        Guid actorUserId,
        string actorEmail)
    {
        if (!ValidateRequest(request, out var validationError))
        {
            return new AdminNoShowRiskPolicyUpdateResultDto(
                false,
                null,
                "validation_error",
                validationError);
        }

        var activePolicy = await _policyRepository.GetActiveAsync();
        if (activePolicy == null)
        {
            return new AdminNoShowRiskPolicyUpdateResultDto(
                false,
                null,
                "not_found",
                "Politica ativa de risco de no-show nao encontrada.");
        }

        var before = Snapshot(activePolicy);

        activePolicy.LookbackDays = request.LookbackDays;
        activePolicy.MaxHistoryEventsPerActor = request.MaxHistoryEventsPerActor;
        activePolicy.MinClientHistoryRiskEvents = request.MinClientHistoryRiskEvents;
        activePolicy.MinProviderHistoryRiskEvents = request.MinProviderHistoryRiskEvents;
        activePolicy.WeightClientNotConfirmed = request.WeightClientNotConfirmed;
        activePolicy.WeightProviderNotConfirmed = request.WeightProviderNotConfirmed;
        activePolicy.WeightBothNotConfirmedBonus = request.WeightBothNotConfirmedBonus;
        activePolicy.WeightWindowWithin24Hours = request.WeightWindowWithin24Hours;
        activePolicy.WeightWindowWithin6Hours = request.WeightWindowWithin6Hours;
        activePolicy.WeightWindowWithin2Hours = request.WeightWindowWithin2Hours;
        activePolicy.WeightClientHistoryRisk = request.WeightClientHistoryRisk;
        activePolicy.WeightProviderHistoryRisk = request.WeightProviderHistoryRisk;
        activePolicy.LowThresholdScore = request.LowThresholdScore;
        activePolicy.MediumThresholdScore = request.MediumThresholdScore;
        activePolicy.HighThresholdScore = request.HighThresholdScore;
        activePolicy.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        activePolicy.UpdatedAt = DateTime.UtcNow;

        await _policyRepository.UpdateAsync(activePolicy);

        var after = Snapshot(activePolicy);
        await WriteAuditAsync(actorUserId, actorEmail, activePolicy.Id, before, after);

        _logger.LogInformation(
            "No-show risk policy updated by admin. ActorUserId={ActorUserId}, PolicyId={PolicyId}, HighThreshold={HighThreshold}, MediumThreshold={MediumThreshold}",
            actorUserId,
            activePolicy.Id,
            activePolicy.HighThresholdScore,
            activePolicy.MediumThresholdScore);

        return new AdminNoShowRiskPolicyUpdateResultDto(true, MapDto(activePolicy));
    }

    private async Task WriteAuditAsync(
        Guid actorUserId,
        string actorEmail,
        Guid policyId,
        object before,
        object after)
    {
        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = actorUserId,
            ActorEmail = actorEmail,
            Action = "ServiceAppointmentNoShowRiskPolicyUpdated",
            TargetType = "ServiceAppointmentNoShowRiskPolicy",
            TargetId = policyId,
            Metadata = JsonSerializer.Serialize(new
            {
                before,
                after
            })
        });
    }

    private static object Snapshot(ServiceAppointmentNoShowRiskPolicy policy)
    {
        return new
        {
            policy.Id,
            policy.LookbackDays,
            policy.MaxHistoryEventsPerActor,
            policy.MinClientHistoryRiskEvents,
            policy.MinProviderHistoryRiskEvents,
            policy.WeightClientNotConfirmed,
            policy.WeightProviderNotConfirmed,
            policy.WeightBothNotConfirmedBonus,
            policy.WeightWindowWithin24Hours,
            policy.WeightWindowWithin6Hours,
            policy.WeightWindowWithin2Hours,
            policy.WeightClientHistoryRisk,
            policy.WeightProviderHistoryRisk,
            policy.LowThresholdScore,
            policy.MediumThresholdScore,
            policy.HighThresholdScore,
            policy.Notes
        };
    }

    private static bool ValidateRequest(AdminUpdateNoShowRiskPolicyRequestDto request, out string? validationError)
    {
        validationError = null;

        if (!Between(request.LookbackDays, 1, 365))
        {
            validationError = "LookbackDays deve estar entre 1 e 365.";
            return false;
        }

        if (!Between(request.MaxHistoryEventsPerActor, 1, 200))
        {
            validationError = "MaxHistoryEventsPerActor deve estar entre 1 e 200.";
            return false;
        }

        if (!Between(request.MinClientHistoryRiskEvents, 1, 50))
        {
            validationError = "MinClientHistoryRiskEvents deve estar entre 1 e 50.";
            return false;
        }

        if (!Between(request.MinProviderHistoryRiskEvents, 1, 50))
        {
            validationError = "MinProviderHistoryRiskEvents deve estar entre 1 e 50.";
            return false;
        }

        if (!Between(request.WeightClientNotConfirmed, 0, 100) ||
            !Between(request.WeightProviderNotConfirmed, 0, 100) ||
            !Between(request.WeightBothNotConfirmedBonus, 0, 100) ||
            !Between(request.WeightWindowWithin24Hours, 0, 100) ||
            !Between(request.WeightWindowWithin6Hours, 0, 100) ||
            !Between(request.WeightWindowWithin2Hours, 0, 100) ||
            !Between(request.WeightClientHistoryRisk, 0, 100) ||
            !Between(request.WeightProviderHistoryRisk, 0, 100))
        {
            validationError = "Todos os pesos devem estar entre 0 e 100.";
            return false;
        }

        if (!Between(request.LowThresholdScore, 0, 100) ||
            !Between(request.MediumThresholdScore, 0, 100) ||
            !Between(request.HighThresholdScore, 0, 100))
        {
            validationError = "Thresholds devem estar entre 0 e 100.";
            return false;
        }

        if (request.LowThresholdScore > request.MediumThresholdScore ||
            request.MediumThresholdScore > request.HighThresholdScore)
        {
            validationError = "Thresholds devem respeitar a ordem: low <= medium <= high.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.Notes) && request.Notes.Trim().Length > 1000)
        {
            validationError = "Notes deve ter no maximo 1000 caracteres.";
            return false;
        }

        return true;
    }

    private static bool Between(int value, int min, int max)
    {
        return value >= min && value <= max;
    }

    private static AdminNoShowRiskPolicyDto MapDto(ServiceAppointmentNoShowRiskPolicy policy)
    {
        return new AdminNoShowRiskPolicyDto(
            policy.Id,
            policy.Name,
            policy.IsActive,
            policy.LookbackDays,
            policy.MaxHistoryEventsPerActor,
            policy.MinClientHistoryRiskEvents,
            policy.MinProviderHistoryRiskEvents,
            policy.WeightClientNotConfirmed,
            policy.WeightProviderNotConfirmed,
            policy.WeightBothNotConfirmedBonus,
            policy.WeightWindowWithin24Hours,
            policy.WeightWindowWithin6Hours,
            policy.WeightWindowWithin2Hours,
            policy.WeightClientHistoryRisk,
            policy.WeightProviderHistoryRisk,
            policy.LowThresholdScore,
            policy.MediumThresholdScore,
            policy.HighThresholdScore,
            policy.Notes,
            policy.CreatedAt,
            policy.UpdatedAt);
    }
}
