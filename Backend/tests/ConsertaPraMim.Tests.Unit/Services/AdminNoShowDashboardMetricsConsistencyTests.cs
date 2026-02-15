using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminNoShowDashboardMetricsConsistencyTests
{
    [Fact]
    public async Task GetDashboardAsync_ShouldCalculateRatesAndRoundUsingBusinessRule()
    {
        var fromUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        var repositoryMock = new Mock<IAdminNoShowDashboardRepository>(MockBehavior.Strict);

        repositoryMock
            .Setup(r => r.GetKpisAsync(
                fromUtc,
                toUtc,
                "Santos",
                "Hidraulica",
                ServiceAppointmentNoShowRiskLevel.High,
                24))
            .ReturnsAsync(new AdminNoShowDashboardKpiReadModel(
                BaseAppointments: 13,
                NoShowAppointments: 2,
                AttendanceAppointments: 9,
                DualPresenceConfirmedAppointments: 5,
                HighRiskAppointments: 7,
                HighRiskConvertedAppointments: 3,
                OpenQueueItems: 8,
                HighRiskOpenQueueItems: 5,
                AverageQueueAgeMinutes: 12.35d));

        repositoryMock
            .Setup(r => r.GetBreakdownByCategoryAsync(
                fromUtc,
                toUtc,
                "Santos",
                ServiceAppointmentNoShowRiskLevel.High,
                24))
            .ReturnsAsync(new List<AdminNoShowBreakdownReadModel>
            {
                new("Hidraulica", 3, 1, 2)
            });

        repositoryMock
            .Setup(r => r.GetBreakdownByCityAsync(
                fromUtc,
                toUtc,
                "Hidraulica",
                ServiceAppointmentNoShowRiskLevel.High,
                24))
            .ReturnsAsync(new List<AdminNoShowBreakdownReadModel>
            {
                new("Santos", 4, 1, 1)
            });

        repositoryMock
            .Setup(r => r.GetOpenRiskQueueAsync(
                fromUtc,
                toUtc,
                "Santos",
                "Hidraulica",
                ServiceAppointmentNoShowRiskLevel.High,
                50))
            .ReturnsAsync(Array.Empty<AdminNoShowRiskQueueItemReadModel>());

        var service = new AdminNoShowDashboardService(repositoryMock.Object);

        var result = await service.GetDashboardAsync(new AdminNoShowDashboardQueryDto(
            FromUtc: fromUtc,
            ToUtc: toUtc,
            City: "Santos",
            Category: "Hidraulica",
            RiskLevel: "High",
            QueueTake: 50,
            CancellationNoShowWindowHours: 24));

        Assert.Equal(15.4m, result.NoShowRatePercent);
        Assert.Equal(69.2m, result.AttendanceRatePercent);
        Assert.Equal(38.5m, result.DualPresenceConfirmationRatePercent);
        Assert.Equal(42.9m, result.HighRiskConversionRatePercent);
        Assert.Equal(12.4d, result.AverageQueueAgeMinutes);
        Assert.Single(result.NoShowByCategory);
        Assert.Equal(33.3m, result.NoShowByCategory[0].NoShowRatePercent);
        Assert.Single(result.NoShowByCity);
        Assert.Equal(25.0m, result.NoShowByCity[0].NoShowRatePercent);
        repositoryMock.VerifyAll();
    }

    [Fact]
    public async Task GetDashboardAsync_ShouldReturnZeroRates_WhenDenominatorIsZero()
    {
        var fromUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc);
        var repositoryMock = new Mock<IAdminNoShowDashboardRepository>(MockBehavior.Strict);

        repositoryMock
            .Setup(r => r.GetKpisAsync(
                fromUtc,
                toUtc,
                null,
                null,
                null,
                24))
            .ReturnsAsync(new AdminNoShowDashboardKpiReadModel(
                BaseAppointments: 0,
                NoShowAppointments: 3,
                AttendanceAppointments: 5,
                DualPresenceConfirmedAppointments: 2,
                HighRiskAppointments: 0,
                HighRiskConvertedAppointments: 1,
                OpenQueueItems: 0,
                HighRiskOpenQueueItems: 0,
                AverageQueueAgeMinutes: 0d));

        repositoryMock
            .Setup(r => r.GetBreakdownByCategoryAsync(
                fromUtc,
                toUtc,
                null,
                null,
                24))
            .ReturnsAsync(new List<AdminNoShowBreakdownReadModel>
            {
                new("Sem categoria", 0, 2, 0)
            });

        repositoryMock
            .Setup(r => r.GetBreakdownByCityAsync(
                fromUtc,
                toUtc,
                null,
                null,
                24))
            .ReturnsAsync(new List<AdminNoShowBreakdownReadModel>
            {
                new("Sem cidade", 0, 1, 0)
            });

        repositoryMock
            .Setup(r => r.GetOpenRiskQueueAsync(
                fromUtc,
                toUtc,
                null,
                null,
                null,
                50))
            .ReturnsAsync(Array.Empty<AdminNoShowRiskQueueItemReadModel>());

        var service = new AdminNoShowDashboardService(repositoryMock.Object);

        var result = await service.GetDashboardAsync(new AdminNoShowDashboardQueryDto(
            FromUtc: fromUtc,
            ToUtc: toUtc));

        Assert.Equal(0m, result.NoShowRatePercent);
        Assert.Equal(0m, result.AttendanceRatePercent);
        Assert.Equal(0m, result.DualPresenceConfirmationRatePercent);
        Assert.Equal(0m, result.HighRiskConversionRatePercent);
        Assert.Equal(0m, result.NoShowByCategory[0].NoShowRatePercent);
        Assert.Equal(0m, result.NoShowByCity[0].NoShowRatePercent);
        repositoryMock.VerifyAll();
    }

    [Fact]
    public async Task GetDashboardAsync_ShouldNormalizeDateRangeAndClampFilters()
    {
        var expectedFromUtc = new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc);
        var expectedToUtc = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc);
        var repositoryMock = new Mock<IAdminNoShowDashboardRepository>(MockBehavior.Strict);

        repositoryMock
            .Setup(r => r.GetKpisAsync(
                expectedFromUtc,
                expectedToUtc,
                "Santos",
                "Eletrica",
                ServiceAppointmentNoShowRiskLevel.High,
                1))
            .ReturnsAsync(new AdminNoShowDashboardKpiReadModel(
                BaseAppointments: 1,
                NoShowAppointments: 0,
                AttendanceAppointments: 1,
                DualPresenceConfirmedAppointments: 1,
                HighRiskAppointments: 1,
                HighRiskConvertedAppointments: 1,
                OpenQueueItems: 0,
                HighRiskOpenQueueItems: 0,
                AverageQueueAgeMinutes: 0d));

        repositoryMock
            .Setup(r => r.GetBreakdownByCategoryAsync(
                expectedFromUtc,
                expectedToUtc,
                "Santos",
                ServiceAppointmentNoShowRiskLevel.High,
                1))
            .ReturnsAsync(Array.Empty<AdminNoShowBreakdownReadModel>());

        repositoryMock
            .Setup(r => r.GetBreakdownByCityAsync(
                expectedFromUtc,
                expectedToUtc,
                "Eletrica",
                ServiceAppointmentNoShowRiskLevel.High,
                1))
            .ReturnsAsync(Array.Empty<AdminNoShowBreakdownReadModel>());

        repositoryMock
            .Setup(r => r.GetOpenRiskQueueAsync(
                expectedFromUtc,
                expectedToUtc,
                "Santos",
                "Eletrica",
                ServiceAppointmentNoShowRiskLevel.High,
                500))
            .ReturnsAsync(Array.Empty<AdminNoShowRiskQueueItemReadModel>());

        var service = new AdminNoShowDashboardService(repositoryMock.Object);

        var result = await service.GetDashboardAsync(new AdminNoShowDashboardQueryDto(
            FromUtc: expectedToUtc,
            ToUtc: expectedFromUtc,
            City: "Santos",
            Category: "Eletrica",
            RiskLevel: "high",
            QueueTake: 999,
            CancellationNoShowWindowHours: -10));

        Assert.Equal(expectedFromUtc, result.FromUtc);
        Assert.Equal(expectedToUtc, result.ToUtc);
        Assert.Equal("High", result.RiskLevelFilter);
        repositoryMock.VerifyAll();
    }

    [Fact]
    public async Task GetDashboardAsync_ShouldIgnoreInvalidRiskLevelFilter()
    {
        var fromUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 2, 2, 0, 0, 0, DateTimeKind.Utc);
        var repositoryMock = new Mock<IAdminNoShowDashboardRepository>(MockBehavior.Strict);

        repositoryMock
            .Setup(r => r.GetKpisAsync(
                fromUtc,
                toUtc,
                null,
                null,
                null,
                24))
            .ReturnsAsync(new AdminNoShowDashboardKpiReadModel(
                BaseAppointments: 1,
                NoShowAppointments: 0,
                AttendanceAppointments: 1,
                DualPresenceConfirmedAppointments: 1,
                HighRiskAppointments: 0,
                HighRiskConvertedAppointments: 0,
                OpenQueueItems: 0,
                HighRiskOpenQueueItems: 0,
                AverageQueueAgeMinutes: 0d));

        repositoryMock
            .Setup(r => r.GetBreakdownByCategoryAsync(
                fromUtc,
                toUtc,
                null,
                null,
                24))
            .ReturnsAsync(Array.Empty<AdminNoShowBreakdownReadModel>());

        repositoryMock
            .Setup(r => r.GetBreakdownByCityAsync(
                fromUtc,
                toUtc,
                null,
                null,
                24))
            .ReturnsAsync(Array.Empty<AdminNoShowBreakdownReadModel>());

        repositoryMock
            .Setup(r => r.GetOpenRiskQueueAsync(
                fromUtc,
                toUtc,
                null,
                null,
                null,
                50))
            .ReturnsAsync(Array.Empty<AdminNoShowRiskQueueItemReadModel>());

        var service = new AdminNoShowDashboardService(repositoryMock.Object);

        var result = await service.GetDashboardAsync(new AdminNoShowDashboardQueryDto(
            FromUtc: fromUtc,
            ToUtc: toUtc,
            RiskLevel: "nao_existe"));

        Assert.Null(result.RiskLevelFilter);
        repositoryMock.VerifyAll();
    }
}
