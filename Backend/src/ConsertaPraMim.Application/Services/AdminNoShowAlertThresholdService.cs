using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConsertaPraMim.Application.Services;

public class AdminNoShowAlertThresholdService : IAdminNoShowAlertThresholdService
{
    private readonly INoShowAlertThresholdConfigurationRepository _configurationRepository;
    private readonly IAdminAuditLogRepository _adminAuditLogRepository;
    private readonly ILogger<AdminNoShowAlertThresholdService> _logger;

    public AdminNoShowAlertThresholdService(
        INoShowAlertThresholdConfigurationRepository configurationRepository,
        IAdminAuditLogRepository adminAuditLogRepository,
        ILogger<AdminNoShowAlertThresholdService>? logger = null)
    {
        _configurationRepository = configurationRepository;
        _adminAuditLogRepository = adminAuditLogRepository;
        _logger = logger ?? NullLogger<AdminNoShowAlertThresholdService>.Instance;
    }

    public async Task<AdminNoShowAlertThresholdDto?> GetActiveAsync()
    {
        var configuration = await _configurationRepository.GetActiveAsync();
        return configuration == null ? null : MapDto(configuration);
    }

    public async Task<AdminNoShowAlertThresholdUpdateResultDto> UpdateActiveAsync(
        AdminUpdateNoShowAlertThresholdRequestDto request,
        Guid actorUserId,
        string actorEmail)
    {
        if (!ValidateRequest(request, out var validationError))
        {
            return new AdminNoShowAlertThresholdUpdateResultDto(
                false,
                null,
                "validation_error",
                validationError);
        }

        var activeConfiguration = await _configurationRepository.GetActiveAsync();
        if (activeConfiguration == null)
        {
            return new AdminNoShowAlertThresholdUpdateResultDto(
                false,
                null,
                "not_found",
                "Configuracao ativa de threshold de no-show nao encontrada.");
        }

        var before = Snapshot(activeConfiguration);

        activeConfiguration.NoShowRateWarningPercent = Decimal.Round(request.NoShowRateWarningPercent, 2);
        activeConfiguration.NoShowRateCriticalPercent = Decimal.Round(request.NoShowRateCriticalPercent, 2);
        activeConfiguration.HighRiskQueueWarningCount = request.HighRiskQueueWarningCount;
        activeConfiguration.HighRiskQueueCriticalCount = request.HighRiskQueueCriticalCount;
        activeConfiguration.ReminderSendSuccessWarningPercent = Decimal.Round(request.ReminderSendSuccessWarningPercent, 2);
        activeConfiguration.ReminderSendSuccessCriticalPercent = Decimal.Round(request.ReminderSendSuccessCriticalPercent, 2);
        activeConfiguration.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        activeConfiguration.UpdatedAt = DateTime.UtcNow;

        await _configurationRepository.UpdateAsync(activeConfiguration);

        var after = Snapshot(activeConfiguration);
        await WriteAuditAsync(actorUserId, actorEmail, activeConfiguration.Id, before, after);

        _logger.LogInformation(
            "No-show alert thresholds updated. ActorUserId={ActorUserId}, ThresholdId={ThresholdId}, NoShowCritical={NoShowCritical}, QueueCritical={QueueCritical}",
            actorUserId,
            activeConfiguration.Id,
            activeConfiguration.NoShowRateCriticalPercent,
            activeConfiguration.HighRiskQueueCriticalCount);

        return new AdminNoShowAlertThresholdUpdateResultDto(true, MapDto(activeConfiguration));
    }

    private async Task WriteAuditAsync(
        Guid actorUserId,
        string actorEmail,
        Guid thresholdId,
        object before,
        object after)
    {
        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = actorUserId,
            ActorEmail = actorEmail,
            Action = "NoShowAlertThresholdConfigurationUpdated",
            TargetType = "NoShowAlertThresholdConfiguration",
            TargetId = thresholdId,
            Metadata = JsonSerializer.Serialize(new
            {
                before,
                after
            })
        });
    }

    private static object Snapshot(NoShowAlertThresholdConfiguration configuration)
    {
        return new
        {
            configuration.Id,
            configuration.NoShowRateWarningPercent,
            configuration.NoShowRateCriticalPercent,
            configuration.HighRiskQueueWarningCount,
            configuration.HighRiskQueueCriticalCount,
            configuration.ReminderSendSuccessWarningPercent,
            configuration.ReminderSendSuccessCriticalPercent,
            configuration.Notes
        };
    }

    private static bool ValidateRequest(AdminUpdateNoShowAlertThresholdRequestDto request, out string? validationError)
    {
        validationError = null;

        if (!Between(request.NoShowRateWarningPercent, 0m, 100m) ||
            !Between(request.NoShowRateCriticalPercent, 0m, 100m))
        {
            validationError = "Threshold de taxa de no-show deve estar entre 0 e 100%.";
            return false;
        }

        if (request.NoShowRateWarningPercent > request.NoShowRateCriticalPercent)
        {
            validationError = "No-show: warning deve ser menor ou igual ao critical.";
            return false;
        }

        if (!Between(request.HighRiskQueueWarningCount, 0, 100000) ||
            !Between(request.HighRiskQueueCriticalCount, 0, 100000))
        {
            validationError = "Threshold da fila de risco deve estar entre 0 e 100000.";
            return false;
        }

        if (request.HighRiskQueueWarningCount > request.HighRiskQueueCriticalCount)
        {
            validationError = "Fila de risco: warning deve ser menor ou igual ao critical.";
            return false;
        }

        if (!Between(request.ReminderSendSuccessWarningPercent, 0m, 100m) ||
            !Between(request.ReminderSendSuccessCriticalPercent, 0m, 100m))
        {
            validationError = "Threshold de sucesso de lembrete deve estar entre 0 e 100%.";
            return false;
        }

        if (request.ReminderSendSuccessCriticalPercent > request.ReminderSendSuccessWarningPercent)
        {
            validationError = "Sucesso de lembrete: critical deve ser menor ou igual ao warning.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.Notes) && request.Notes.Trim().Length > 1000)
        {
            validationError = "Notes deve ter no maximo 1000 caracteres.";
            return false;
        }

        return true;
    }

    private static bool Between(decimal value, decimal min, decimal max)
    {
        return value >= min && value <= max;
    }

    private static bool Between(int value, int min, int max)
    {
        return value >= min && value <= max;
    }

    private static AdminNoShowAlertThresholdDto MapDto(NoShowAlertThresholdConfiguration configuration)
    {
        return new AdminNoShowAlertThresholdDto(
            configuration.Id,
            configuration.Name,
            configuration.IsActive,
            configuration.NoShowRateWarningPercent,
            configuration.NoShowRateCriticalPercent,
            configuration.HighRiskQueueWarningCount,
            configuration.HighRiskQueueCriticalCount,
            configuration.ReminderSendSuccessWarningPercent,
            configuration.ReminderSendSuccessCriticalPercent,
            configuration.Notes,
            configuration.CreatedAt,
            configuration.UpdatedAt);
    }
}
