using ConsertaPraMim.Domain.Common;

namespace ConsertaPraMim.Domain.Entities;

public class NoShowAlertThresholdConfiguration : BaseEntity
{
    public string Name { get; set; } = "Threshold padrao no-show";
    public bool IsActive { get; set; } = true;

    public decimal NoShowRateWarningPercent { get; set; } = 20m;
    public decimal NoShowRateCriticalPercent { get; set; } = 30m;

    public int HighRiskQueueWarningCount { get; set; } = 10;
    public int HighRiskQueueCriticalCount { get; set; } = 20;

    public decimal ReminderSendSuccessWarningPercent { get; set; } = 95m;
    public decimal ReminderSendSuccessCriticalPercent { get; set; } = 90m;

    public string? Notes { get; set; }
}
