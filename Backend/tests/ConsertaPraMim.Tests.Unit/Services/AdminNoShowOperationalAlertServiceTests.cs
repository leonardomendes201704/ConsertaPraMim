using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminNoShowOperationalAlertServiceTests
{
    /// <summary>
    /// Cenario: indicadores operacionais ultrapassam thresholds críticos de no-show e exigem alerta imediato.
    /// Passos: configura thresholds ativos, simula KPIs críticos, identifica admins ativos e executa EvaluateAndNotifyAsync.
    /// Resultado esperado: envio de alerta crítico para todos os admins ativos e registro de auditoria do disparo.
    /// </summary>
    [Fact(DisplayName = "Admin no show operational alert servico | Evaluate e notify | Deve enviar critical alert para active admins quando threshold exceeded")]
    public async Task EvaluateAndNotifyAsync_ShouldSendCriticalAlertToActiveAdmins_WhenThresholdIsExceeded()
    {
        var thresholdRepository = new Mock<INoShowAlertThresholdConfigurationRepository>();
        var dashboardRepository = new Mock<IAdminNoShowDashboardRepository>();
        var reminderRepository = new Mock<IAppointmentReminderDispatchRepository>();
        var userRepository = new Mock<IUserRepository>();
        var notificationService = new Mock<INotificationService>();
        var auditRepository = new Mock<IAdminAuditLogRepository>();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var adminAId = Guid.NewGuid();
        var adminBId = Guid.NewGuid();

        thresholdRepository
            .Setup(r => r.GetActiveAsync())
            .ReturnsAsync(new NoShowAlertThresholdConfiguration
            {
                Id = Guid.NewGuid(),
                IsActive = true,
                NoShowRateWarningPercent = 20m,
                NoShowRateCriticalPercent = 30m,
                HighRiskQueueWarningCount = 10,
                HighRiskQueueCriticalCount = 20,
                ReminderSendSuccessWarningPercent = 95m,
                ReminderSendSuccessCriticalPercent = 90m
            });

        dashboardRepository
            .Setup(r => r.GetKpisAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                null,
                null,
                It.IsAny<int>()))
            .ReturnsAsync(new AdminNoShowDashboardKpiReadModel(
                BaseAppointments: 10,
                NoShowAppointments: 4,
                AttendanceAppointments: 6,
                DualPresenceConfirmedAppointments: 5,
                HighRiskAppointments: 3,
                HighRiskConvertedAppointments: 1,
                OpenQueueItems: 8,
                HighRiskOpenQueueItems: 6,
                AverageQueueAgeMinutes: 12));

        reminderRepository
            .Setup(r => r.CountAsync(
                null,
                It.IsAny<AppointmentReminderDispatchStatus?>(),
                null,
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>()))
            .ReturnsAsync((Guid? _, AppointmentReminderDispatchStatus? status, AppointmentReminderChannel? _, DateTime? _, DateTime? _) =>
                status == AppointmentReminderDispatchStatus.Sent ? 20 : 0);

        userRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<User>
            {
                new() { Id = adminAId, Role = UserRole.Admin, IsActive = true },
                new() { Id = adminBId, Role = UserRole.Admin, IsActive = true },
                new() { Id = Guid.NewGuid(), Role = UserRole.Provider, IsActive = true }
            });

        var configuration = BuildConfiguration(cooldownMinutes: 60);
        var service = new AdminNoShowOperationalAlertService(
            thresholdRepository.Object,
            dashboardRepository.Object,
            reminderRepository.Object,
            userRepository.Object,
            notificationService.Object,
            auditRepository.Object,
            memoryCache,
            configuration,
            NullLogger<AdminNoShowOperationalAlertService>.Instance);

        var sent = await service.EvaluateAndNotifyAsync();

        Assert.Equal(2, sent);
        notificationService.Verify(n => n.SendNotificationAsync(
                It.Is<string>(recipient => recipient == adminAId.ToString("N") || recipient == adminBId.ToString("N")),
                It.Is<string>(subject => subject.Contains("CRITICO", StringComparison.OrdinalIgnoreCase)),
                It.Is<string>(message => message.Contains("Taxa de no-show", StringComparison.OrdinalIgnoreCase)),
                It.Is<string?>(url => url == "/AdminHome")),
            Times.Exactly(2));
        auditRepository.Verify(r => r.AddAsync(It.Is<AdminAuditLog>(a =>
            a.Action == "NoShowOperationalAlertDispatched" &&
            a.ActorEmail == "system@internal")), Times.Once);
    }

    /// <summary>
    /// Cenario: mesma condição crítica ocorre novamente dentro da janela de cooldown.
    /// Passos: dispara avaliação duas vezes consecutivas com cooldown configurado e mesmo cenário de risco.
    /// Resultado esperado: primeiro envio ocorre normalmente e segundo envio é suprimido pelo controle de cooldown.
    /// </summary>
    [Fact(DisplayName = "Admin no show operational alert servico | Evaluate e notify | Deve respect cooldown quando alert was recently sent")]
    public async Task EvaluateAndNotifyAsync_ShouldRespectCooldown_WhenAlertWasRecentlySent()
    {
        var thresholdRepository = new Mock<INoShowAlertThresholdConfigurationRepository>();
        var dashboardRepository = new Mock<IAdminNoShowDashboardRepository>();
        var reminderRepository = new Mock<IAppointmentReminderDispatchRepository>();
        var userRepository = new Mock<IUserRepository>();
        var notificationService = new Mock<INotificationService>();
        var auditRepository = new Mock<IAdminAuditLogRepository>();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());

        var adminId = Guid.NewGuid();

        thresholdRepository
            .Setup(r => r.GetActiveAsync())
            .ReturnsAsync(new NoShowAlertThresholdConfiguration
            {
                Id = Guid.NewGuid(),
                IsActive = true,
                NoShowRateWarningPercent = 20m,
                NoShowRateCriticalPercent = 30m,
                HighRiskQueueWarningCount = 10,
                HighRiskQueueCriticalCount = 20,
                ReminderSendSuccessWarningPercent = 95m,
                ReminderSendSuccessCriticalPercent = 90m
            });

        dashboardRepository
            .Setup(r => r.GetKpisAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                null,
                null,
                null,
                It.IsAny<int>()))
            .ReturnsAsync(new AdminNoShowDashboardKpiReadModel(
                BaseAppointments: 10,
                NoShowAppointments: 4,
                AttendanceAppointments: 6,
                DualPresenceConfirmedAppointments: 5,
                HighRiskAppointments: 3,
                HighRiskConvertedAppointments: 1,
                OpenQueueItems: 8,
                HighRiskOpenQueueItems: 6,
                AverageQueueAgeMinutes: 12));

        reminderRepository
            .Setup(r => r.CountAsync(
                null,
                It.IsAny<AppointmentReminderDispatchStatus?>(),
                null,
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>()))
            .ReturnsAsync((Guid? _, AppointmentReminderDispatchStatus? status, AppointmentReminderChannel? _, DateTime? _, DateTime? _) =>
                status == AppointmentReminderDispatchStatus.Sent ? 20 : 0);

        userRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<User>
            {
                new() { Id = adminId, Role = UserRole.Admin, IsActive = true }
            });

        var configuration = BuildConfiguration(cooldownMinutes: 120);
        var service = new AdminNoShowOperationalAlertService(
            thresholdRepository.Object,
            dashboardRepository.Object,
            reminderRepository.Object,
            userRepository.Object,
            notificationService.Object,
            auditRepository.Object,
            memoryCache,
            configuration,
            NullLogger<AdminNoShowOperationalAlertService>.Instance);

        var firstSend = await service.EvaluateAndNotifyAsync();
        var secondSend = await service.EvaluateAndNotifyAsync();

        Assert.Equal(1, firstSend);
        Assert.Equal(0, secondSend);
        notificationService.Verify(n => n.SendNotificationAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>()), Times.Once);
        auditRepository.Verify(r => r.AddAsync(It.IsAny<AdminAuditLog>()), Times.Once);
    }

    private static IConfiguration BuildConfiguration(int cooldownMinutes)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ServiceAppointments:NoShowRisk:OperationalAlerts:Enabled"] = "true",
                ["ServiceAppointments:NoShowRisk:OperationalAlerts:EvaluationWindowHours"] = "24",
                ["ServiceAppointments:NoShowRisk:OperationalAlerts:CancellationNoShowWindowHours"] = "24",
                ["ServiceAppointments:NoShowRisk:OperationalAlerts:CooldownMinutes"] = cooldownMinutes.ToString()
            })
            .Build();
    }
}
