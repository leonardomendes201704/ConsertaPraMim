using System.Globalization;
using System.Text;
using System.Text.Json;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConsertaPraMim.Application.Services;

public class AdminNoShowOperationalAlertService : IAdminNoShowOperationalAlertService
{
    private const string CooldownCacheKeyPrefix = "no-show-operational-alert:";
    private const string SystemActorEmail = "system@internal";
    private static readonly Guid SystemActorUserId = Guid.Empty;

    private readonly INoShowAlertThresholdConfigurationRepository _thresholdRepository;
    private readonly IAdminNoShowDashboardRepository _dashboardRepository;
    private readonly IAppointmentReminderDispatchRepository _reminderDispatchRepository;
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly IAdminAuditLogRepository _adminAuditLogRepository;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<AdminNoShowOperationalAlertService> _logger;
    private readonly IConfiguration _configuration;

    public AdminNoShowOperationalAlertService(
        INoShowAlertThresholdConfigurationRepository thresholdRepository,
        IAdminNoShowDashboardRepository dashboardRepository,
        IAppointmentReminderDispatchRepository reminderDispatchRepository,
        IUserRepository userRepository,
        INotificationService notificationService,
        IAdminAuditLogRepository adminAuditLogRepository,
        IMemoryCache memoryCache,
        IConfiguration configuration,
        ILogger<AdminNoShowOperationalAlertService>? logger = null)
    {
        _thresholdRepository = thresholdRepository;
        _dashboardRepository = dashboardRepository;
        _reminderDispatchRepository = reminderDispatchRepository;
        _userRepository = userRepository;
        _notificationService = notificationService;
        _adminAuditLogRepository = adminAuditLogRepository;
        _memoryCache = memoryCache;
        _configuration = configuration;
        _logger = logger ?? NullLogger<AdminNoShowOperationalAlertService>.Instance;
    }

    public async Task<int> EvaluateAndNotifyAsync(CancellationToken cancellationToken = default)
    {
        if (!ParseBoolean(_configuration["ServiceAppointments:NoShowRisk:OperationalAlerts:Enabled"], true))
        {
            return 0;
        }

        var threshold = await _thresholdRepository.GetActiveAsync();
        if (threshold == null)
        {
            _logger.LogWarning("Operational no-show alerts skipped because there is no active threshold configuration.");
            return 0;
        }

        var nowUtc = DateTime.UtcNow;
        var evaluationWindowHours = ParseInt(_configuration["ServiceAppointments:NoShowRisk:OperationalAlerts:EvaluationWindowHours"], 24, 1, 720);
        var cancellationNoShowWindowHours = ParseInt(_configuration["ServiceAppointments:NoShowRisk:OperationalAlerts:CancellationNoShowWindowHours"], 24, 1, 168);
        var cooldownMinutes = ParseInt(_configuration["ServiceAppointments:NoShowRisk:OperationalAlerts:CooldownMinutes"], 60, 1, 1440);

        var fromUtc = nowUtc.AddHours(-evaluationWindowHours);
        var toUtc = nowUtc;

        var kpis = await _dashboardRepository.GetKpisAsync(
            fromUtc,
            toUtc,
            cityFilter: null,
            categoryFilter: null,
            riskLevelFilter: null,
            cancellationNoShowWindowHours);

        var noShowRate = CalculateRate(kpis.NoShowAppointments, kpis.BaseAppointments);
        var highRiskQueueCount = kpis.HighRiskOpenQueueItems;
        var reminderSuccessRate = await CalculateReminderSuccessRateAsync(fromUtc, toUtc);

        var breaches = new List<AlertBreach>();
        AddNoShowRateBreachIfNeeded(breaches, noShowRate, threshold);
        AddHighRiskQueueBreachIfNeeded(breaches, highRiskQueueCount, threshold);
        AddReminderSuccessBreachIfNeeded(breaches, reminderSuccessRate, threshold);

        if (breaches.Count == 0)
        {
            return 0;
        }

        var highestSeverity = breaches.Any(b => b.Severity == AlertSeverity.Critical)
            ? AlertSeverity.Critical
            : AlertSeverity.Warning;

        var cooldownCacheKey = $"{CooldownCacheKeyPrefix}{highestSeverity.ToString().ToLowerInvariant()}";
        if (_memoryCache.TryGetValue<DateTime>(cooldownCacheKey, out var lastDispatchUtc))
        {
            var elapsed = nowUtc - lastDispatchUtc;
            if (elapsed < TimeSpan.FromMinutes(cooldownMinutes))
            {
                _logger.LogDebug(
                    "Operational no-show alert suppressed by cooldown. Severity={Severity}, ElapsedMinutes={ElapsedMinutes:n1}, CooldownMinutes={CooldownMinutes}",
                    highestSeverity,
                    elapsed.TotalMinutes,
                    cooldownMinutes);
                return 0;
            }
        }

        var recipients = await ResolveRecipientsAsync();
        if (recipients.Count == 0)
        {
            _logger.LogWarning("Operational no-show alert detected but no recipients were resolved.");
            return 0;
        }

        var subject = highestSeverity == AlertSeverity.Critical
            ? "Alerta CRITICO de no-show na operacao"
            : "Alerta de no-show na operacao";
        var message = BuildMessage(breaches, fromUtc, toUtc, evaluationWindowHours, noShowRate, highRiskQueueCount, reminderSuccessRate);
        const string actionUrl = "/AdminHome";

        var sentCount = 0;
        foreach (var recipient in recipients)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await _notificationService.SendNotificationAsync(recipient, subject, message, actionUrl);
                sentCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send operational no-show alert to recipient {Recipient}.", recipient);
            }
        }

        if (sentCount <= 0)
        {
            return 0;
        }

        _memoryCache.Set(cooldownCacheKey, nowUtc, TimeSpan.FromMinutes(cooldownMinutes));

        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = SystemActorUserId,
            ActorEmail = SystemActorEmail,
            Action = "NoShowOperationalAlertDispatched",
            TargetType = "NoShowAlertThresholdConfiguration",
            TargetId = threshold.Id,
            Metadata = JsonSerializer.Serialize(new
            {
                severity = highestSeverity.ToString(),
                periodFromUtc = fromUtc,
                periodToUtc = toUtc,
                evaluationWindowHours,
                breaches = breaches.Select(b => new
                {
                    b.Metric,
                    b.Severity,
                    b.CurrentValue,
                    b.WarningThreshold,
                    b.CriticalThreshold
                }),
                recipients = sentCount
            })
        });

        _logger.LogInformation(
            "Operational no-show alert dispatched. Severity={Severity}, Breaches={BreachCount}, Recipients={Recipients}.",
            highestSeverity,
            breaches.Count,
            sentCount);

        return sentCount;
    }

    private async Task<decimal> CalculateReminderSuccessRateAsync(DateTime fromUtc, DateTime toUtc)
    {
        var sentCount = await _reminderDispatchRepository.CountAsync(
            status: AppointmentReminderDispatchStatus.Sent,
            fromUtc: fromUtc,
            toUtc: toUtc);

        var failedPermanentCount = await _reminderDispatchRepository.CountAsync(
            status: AppointmentReminderDispatchStatus.FailedPermanent,
            fromUtc: fromUtc,
            toUtc: toUtc);

        var failedRetryableCount = await _reminderDispatchRepository.CountAsync(
            status: AppointmentReminderDispatchStatus.FailedRetryable,
            fromUtc: fromUtc,
            toUtc: toUtc);

        var totalFinished = sentCount + failedPermanentCount + failedRetryableCount;
        if (totalFinished <= 0)
        {
            return 100m;
        }

        return Math.Round((decimal)sentCount / totalFinished * 100m, 1, MidpointRounding.AwayFromZero);
    }

    private async Task<HashSet<string>> ResolveRecipientsAsync()
    {
        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var users = await _userRepository.GetAllAsync();

        foreach (var admin in users.Where(u => u.Role == UserRole.Admin && u.IsActive))
        {
            recipients.Add(admin.Id.ToString("N"));
        }

        var configuredRecipients = _configuration
            .GetSection("ServiceAppointments:NoShowRisk:OperationalAlerts:Recipients")
            .Get<string[]>() ?? Array.Empty<string>();

        foreach (var configuredRecipient in configuredRecipients)
        {
            if (string.IsNullOrWhiteSpace(configuredRecipient))
            {
                continue;
            }

            var normalized = configuredRecipient.Trim();
            recipients.Add(Guid.TryParse(normalized, out var recipientUserId)
                ? recipientUserId.ToString("N")
                : normalized.ToLowerInvariant());
        }

        return recipients;
    }

    private static void AddNoShowRateBreachIfNeeded(
        ICollection<AlertBreach> breaches,
        decimal noShowRatePercent,
        NoShowAlertThresholdConfiguration threshold)
    {
        if (noShowRatePercent >= threshold.NoShowRateCriticalPercent)
        {
            breaches.Add(new AlertBreach(
                "NoShowRatePercent",
                AlertSeverity.Critical,
                noShowRatePercent,
                threshold.NoShowRateWarningPercent,
                threshold.NoShowRateCriticalPercent));
            return;
        }

        if (noShowRatePercent >= threshold.NoShowRateWarningPercent)
        {
            breaches.Add(new AlertBreach(
                "NoShowRatePercent",
                AlertSeverity.Warning,
                noShowRatePercent,
                threshold.NoShowRateWarningPercent,
                threshold.NoShowRateCriticalPercent));
        }
    }

    private static void AddHighRiskQueueBreachIfNeeded(
        ICollection<AlertBreach> breaches,
        int highRiskQueueCount,
        NoShowAlertThresholdConfiguration threshold)
    {
        if (highRiskQueueCount >= threshold.HighRiskQueueCriticalCount)
        {
            breaches.Add(new AlertBreach(
                "HighRiskQueueCount",
                AlertSeverity.Critical,
                highRiskQueueCount,
                threshold.HighRiskQueueWarningCount,
                threshold.HighRiskQueueCriticalCount));
            return;
        }

        if (highRiskQueueCount >= threshold.HighRiskQueueWarningCount)
        {
            breaches.Add(new AlertBreach(
                "HighRiskQueueCount",
                AlertSeverity.Warning,
                highRiskQueueCount,
                threshold.HighRiskQueueWarningCount,
                threshold.HighRiskQueueCriticalCount));
        }
    }

    private static void AddReminderSuccessBreachIfNeeded(
        ICollection<AlertBreach> breaches,
        decimal reminderSuccessRate,
        NoShowAlertThresholdConfiguration threshold)
    {
        if (reminderSuccessRate <= threshold.ReminderSendSuccessCriticalPercent)
        {
            breaches.Add(new AlertBreach(
                "ReminderSendSuccessRatePercent",
                AlertSeverity.Critical,
                reminderSuccessRate,
                threshold.ReminderSendSuccessWarningPercent,
                threshold.ReminderSendSuccessCriticalPercent));
            return;
        }

        if (reminderSuccessRate <= threshold.ReminderSendSuccessWarningPercent)
        {
            breaches.Add(new AlertBreach(
                "ReminderSendSuccessRatePercent",
                AlertSeverity.Warning,
                reminderSuccessRate,
                threshold.ReminderSendSuccessWarningPercent,
                threshold.ReminderSendSuccessCriticalPercent));
        }
    }

    private static string BuildMessage(
        IReadOnlyCollection<AlertBreach> breaches,
        DateTime fromUtc,
        DateTime toUtc,
        int windowHours,
        decimal noShowRatePercent,
        int highRiskQueueCount,
        decimal reminderSuccessRatePercent)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Janela analisada: ultimas {windowHours}h ({fromUtc:dd/MM HH:mm} ate {toUtc:dd/MM HH:mm} UTC).");
        sb.AppendLine();
        sb.AppendLine("Metricas atuais:");
        sb.AppendLine($"- Taxa de no-show: {ToPtBr(noShowRatePercent)}%");
        sb.AppendLine($"- Fila de risco alto (aberta): {highRiskQueueCount}");
        sb.AppendLine($"- Sucesso de envio de lembretes: {ToPtBr(reminderSuccessRatePercent)}%");
        sb.AppendLine();
        sb.AppendLine("Thresholds violados:");

        foreach (var breach in breaches.OrderByDescending(b => b.Severity).ThenBy(b => b.Metric, StringComparer.Ordinal))
        {
            sb.AppendLine($"- {MapMetricLabel(breach.Metric)} [{MapSeverityLabel(breach.Severity)}]: atual {ToPtBr(breach.CurrentValue)} | warning {ToPtBr(breach.WarningThreshold)} | critical {ToPtBr(breach.CriticalThreshold)}");
        }

        sb.AppendLine();
        sb.Append("Acesse o painel operacional para atuar no runbook.");
        return sb.ToString();
    }

    private static string MapMetricLabel(string metric)
    {
        return metric switch
        {
            "NoShowRatePercent" => "Taxa de no-show (%)",
            "HighRiskQueueCount" => "Fila de risco alto (qtd)",
            "ReminderSendSuccessRatePercent" => "Sucesso de envio de lembretes (%)",
            _ => metric
        };
    }

    private static string MapSeverityLabel(AlertSeverity severity)
    {
        return severity == AlertSeverity.Critical ? "CRITICO" : "WARNING";
    }

    private static string ToPtBr(decimal value)
    {
        return value.ToString("N1", CultureInfo.GetCultureInfo("pt-BR"));
    }

    private static string ToPtBr(int value)
    {
        return value.ToString("N0", CultureInfo.GetCultureInfo("pt-BR"));
    }

    private static decimal CalculateRate(int numerator, int denominator)
    {
        if (denominator <= 0)
        {
            return 0m;
        }

        return Math.Round((decimal)numerator / denominator * 100m, 1, MidpointRounding.AwayFromZero);
    }

    private static int ParseInt(string? raw, int defaultValue, int min, int max)
    {
        if (!int.TryParse(raw, out var parsed))
        {
            return defaultValue;
        }

        return Math.Clamp(parsed, min, max);
    }

    private static bool ParseBoolean(string? raw, bool defaultValue)
    {
        if (!bool.TryParse(raw, out var parsed))
        {
            return defaultValue;
        }

        return parsed;
    }

    private enum AlertSeverity
    {
        Warning = 1,
        Critical = 2
    }

    private sealed record AlertBreach(
        string Metric,
        AlertSeverity Severity,
        decimal CurrentValue,
        decimal WarningThreshold,
        decimal CriticalThreshold);
}
