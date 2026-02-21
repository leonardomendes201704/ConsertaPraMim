using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminNoShowDashboardServiceTests
{
    [Fact(DisplayName = "Admin no show dashboard servico | Export dashboard csv | Deve include kpis breakdowns e queue rows")]
    public async Task ExportDashboardCsvAsync_ShouldIncludeKpisBreakdownsAndQueueRows()
    {
        var fromUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        var queueItemId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var repositoryMock = new Mock<IAdminNoShowDashboardRepository>(MockBehavior.Strict);

        repositoryMock
            .Setup(r => r.GetKpisAsync(
                fromUtc,
                toUtc,
                "Santos",
                "Hidraulica",
                ServiceAppointmentNoShowRiskLevel.High,
                1))
            .ReturnsAsync(new AdminNoShowDashboardKpiReadModel(
                BaseAppointments: 20,
                NoShowAppointments: 4,
                AttendanceAppointments: 15,
                DualPresenceConfirmedAppointments: 12,
                HighRiskAppointments: 6,
                HighRiskConvertedAppointments: 2,
                OpenQueueItems: 3,
                HighRiskOpenQueueItems: 2,
                AverageQueueAgeMinutes: 25.5d));

        repositoryMock
            .Setup(r => r.GetBreakdownByCategoryAsync(
                fromUtc,
                toUtc,
                "Santos",
                ServiceAppointmentNoShowRiskLevel.High,
                1))
            .ReturnsAsync(new List<AdminNoShowBreakdownReadModel>
            {
                new("Hidraulica", 10, 2, 3)
            });

        repositoryMock
            .Setup(r => r.GetBreakdownByCityAsync(
                fromUtc,
                toUtc,
                "Hidraulica",
                ServiceAppointmentNoShowRiskLevel.High,
                1))
            .ReturnsAsync(new List<AdminNoShowBreakdownReadModel>
            {
                new("Santos", 20, 4, 6)
            });

        repositoryMock
            .Setup(r => r.GetOpenRiskQueueAsync(
                fromUtc,
                toUtc,
                "Santos",
                "Hidraulica",
                ServiceAppointmentNoShowRiskLevel.High,
                500))
            .ReturnsAsync(new List<AdminNoShowRiskQueueItemReadModel>
            {
                new(
                    queueItemId,
                    appointmentId,
                    requestId,
                    "Hidraulica",
                    "Santos",
                    "Prestador 01",
                    "Cliente 01",
                    ServiceAppointmentNoShowRiskLevel.High,
                    90,
                    "both_presence_not_confirmed,window_within_2h",
                    toUtc.AddHours(1),
                    toUtc.AddMinutes(-15),
                    toUtc.AddHours(-1))
            });

        var service = new AdminNoShowDashboardService(repositoryMock.Object);

        var csv = await service.ExportDashboardCsvAsync(new AdminNoShowDashboardQueryDto(
            FromUtc: fromUtc,
            ToUtc: toUtc,
            City: "Santos",
            Category: "Hidraulica",
            RiskLevel: "High",
            QueueTake: 999,
            CancellationNoShowWindowHours: 0));

        Assert.Contains("Section,Name,FromUtc,ToUtc", csv);
        Assert.Contains("Kpi,Resumo", csv);
        Assert.Contains("BreakdownCategory,Hidraulica", csv);
        Assert.Contains("BreakdownCity,Santos", csv);
        Assert.Contains("OpenRiskQueue,Prestador 01 / Cliente 01", csv);
        Assert.Contains(queueItemId.ToString(), csv);
        Assert.Contains("\"both_presence_not_confirmed,window_within_2h\"", csv);

        repositoryMock.VerifyAll();
    }
}
