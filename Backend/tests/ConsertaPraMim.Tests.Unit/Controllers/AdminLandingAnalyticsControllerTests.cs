using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class AdminLandingAnalyticsControllerTests
{
    [Fact(DisplayName = "Admin landing analytics controller | Overview | Deve retornar ok com overview")]
    public async Task GetOverview_ShouldReturnOk()
    {
        var serviceMock = new Mock<IAdminLandingAnalyticsService>();
        var overview = new AdminLandingAnalyticsOverviewDto(
            FromUtc: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc),
            TotalSessions: 1,
            TotalUniqueVisitors: 1,
            SessionsWithGeo: 1,
            TotalHeartbeats: 1,
            TotalActiveSeconds: 15,
            AverageActiveSecondsPerSession: 15,
            AverageMaxScrollPercent: 75,
            SessionsWithClicks: 1,
            TotalClicks: 1,
            LeadModalOpens: 1,
            LeadSubmissions: 1,
            LeadSubmissionRatePercent: 100,
            HeatmapRows: 6,
            HeatmapColumns: 6,
            Page: 1,
            PageSize: 20,
            TotalCount: 1,
            PathBreakdown: [],
            OriginBreakdown: [],
            CountryBreakdown: [],
            RegionBreakdown: [],
            CityBreakdown: [],
            EventBreakdown: [],
            Heatmap: [],
            Sessions: []);

        serviceMock
            .Setup(service => service.GetOverviewAsync(It.IsAny<AdminLandingAnalyticsQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(overview);

        var controller = new AdminLandingAnalyticsController(serviceMock.Object);

        var result = await controller.GetOverview(
            searchTerm: null,
            origin: null,
            path: null,
            countryCode: null,
            region: null,
            city: null,
            includeSuspectedAutomation: false,
            fromUtc: null,
            toUtc: null,
            page: 1,
            pageSize: 20,
            cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(overview, ok.Value);

        serviceMock.Verify(service => service.GetOverviewAsync(
            It.Is<AdminLandingAnalyticsQueryDto>(query => !query.IncludeSuspectedAutomation),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "Admin landing analytics controller | Detalhe | Deve retornar not found quando a sessao nao existir")]
    public async Task GetSessionDetails_ShouldReturnNotFound_WhenSessionDoesNotExist()
    {
        var serviceMock = new Mock<IAdminLandingAnalyticsService>();
        serviceMock
            .Setup(service => service.GetSessionDetailsAsync("session-inexistente", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AdminLandingAnalyticsSessionDetailsDto?)null);

        var controller = new AdminLandingAnalyticsController(serviceMock.Object);

        var result = await controller.GetSessionDetails("session-inexistente", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
