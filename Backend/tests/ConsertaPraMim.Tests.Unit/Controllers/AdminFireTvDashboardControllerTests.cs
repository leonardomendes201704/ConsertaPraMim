using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class AdminFireTvDashboardControllerTests
{
    [Fact(DisplayName = "Admin fire tv dashboard controller | Deve retornar snapshot ok")]
    public async Task Get_ShouldReturnOk()
    {
        var serviceMock = new Mock<IAdminFireTvDashboardService>();
        var payload = new AdminFireTvLandingDashboardDto(
            Enabled: true,
            AppTitle: "ConsertaPraMim TV",
            AppSubtitle: "Landing",
            SelectedRangeDays: 7,
            AllowedRangeDays: [1, 7, 30],
            AutoRefreshSeconds: 30,
            GeneratedAtUtc: new DateTime(2026, 3, 8, 12, 0, 0, DateTimeKind.Utc),
            FromUtc: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 3, 8, 0, 0, 0, DateTimeKind.Utc),
            Kpis:
            [
                new AdminFireTvDashboardKpiDto("totalSessions", "Sessoes", "12", "Periodo", "primary")
            ],
            HeatmapRows: 6,
            HeatmapColumns: 6,
            Heatmap: [],
            TopOrigins: [],
            TopLocalities: [],
            RecentSessions: []);

        serviceMock
            .Setup(service => service.GetLandingDashboardAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payload);

        var controller = new AdminFireTvDashboardController(serviceMock.Object);

        var result = await controller.Get(7, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(payload, ok.Value);
    }
}
