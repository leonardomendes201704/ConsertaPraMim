namespace ConsertaPraMim.Application.DTOs;

public record AdminNoShowAlertThresholdDto(
    Guid Id,
    string Name,
    bool IsActive,
    decimal NoShowRateWarningPercent,
    decimal NoShowRateCriticalPercent,
    int HighRiskQueueWarningCount,
    int HighRiskQueueCriticalCount,
    decimal ReminderSendSuccessWarningPercent,
    decimal ReminderSendSuccessCriticalPercent,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record AdminUpdateNoShowAlertThresholdRequestDto(
    decimal NoShowRateWarningPercent,
    decimal NoShowRateCriticalPercent,
    int HighRiskQueueWarningCount,
    int HighRiskQueueCriticalCount,
    decimal ReminderSendSuccessWarningPercent,
    decimal ReminderSendSuccessCriticalPercent,
    string? Notes = null);

public record AdminNoShowAlertThresholdUpdateResultDto(
    bool Success,
    AdminNoShowAlertThresholdDto? Configuration = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);
