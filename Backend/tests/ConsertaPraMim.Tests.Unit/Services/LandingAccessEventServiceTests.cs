using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Services;

public class LandingAccessEventServiceTests
{
    [Fact(DisplayName = "Landing access event service | Registro | Deve persistir acesso e notificar admins")]
    public async Task RecordAccessAsync_ShouldPersistAccessAndNotifyAdmins()
    {
        var repositoryMock = new Mock<ILandingAccessEventRepository>();
        var notificationServiceMock = new Mock<ILandingAdminNotificationService>();
        var geoIpServiceMock = new Mock<ILandingGeoIpService>();
        LandingAccessEvent? persistedEvent = null;

        repositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<LandingAccessEvent>(), It.IsAny<CancellationToken>()))
            .Callback<LandingAccessEvent, CancellationToken>((accessEvent, _) => persistedEvent = accessEvent)
            .Returns(Task.CompletedTask);

        geoIpServiceMock
            .Setup(service => service.LookupAsync("187.77.48.150", "187.77.48.150", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LandingGeoIpLookupResultDto(
                Status: "resolved",
                Provider: "ipwhois",
                QueryIp: "187.77.48.150",
                Country: "Brasil",
                CountryCode: "BR",
                Region: "Sao Paulo",
                RegionCode: "SP",
                City: "Praia Grande"));

        var service = new LandingAccessEventService(
            repositoryMock.Object,
            notificationServiceMock.Object,
            geoIpServiceMock.Object);

        var request = new NotifyLandingAccessRequestDto(
            VisitorId: "visitor-kpi-001",
            SessionId: "session-kpi-001",
            CurrentUrl: "https://www.consertapramim.com/Prestador",
            Path: "/Prestador",
            Host: "www.consertapramim.com",
            Scheme: "https",
            InitialLeadOrigin: "provider",
            IpAddress: "187.77.48.150",
            ForwardedFor: "187.77.48.150",
            UserAgent: "Mozilla/5.0",
            AcceptLanguage: "pt-BR",
            RefererUrl: "https://www.google.com/");

        await service.RecordAccessAsync(request, CancellationToken.None);

        Assert.NotNull(persistedEvent);
        Assert.Equal("visitor-kpi-001", persistedEvent!.VisitorId);
        Assert.Equal("session-kpi-001", persistedEvent.SessionId);
        Assert.Equal("/Prestador", persistedEvent.Path);
        Assert.Equal(LandingLeadOrigin.Provider, persistedEvent.InitialLeadOrigin);
        Assert.Equal("BR", persistedEvent.GeoCountryCode);
        Assert.Equal("SP", persistedEvent.GeoRegionCode);
        Assert.Equal("Praia Grande", persistedEvent.GeoCity);
        Assert.Contains("visitor-kpi-001", persistedEvent.MetadataJson);
        notificationServiceMock.Verify(
            notifier => notifier.NotifyLandingAccessAsync(
                It.Is<NotifyLandingAccessRequestDto>(dto => dto.VisitorId == "visitor-kpi-001" && dto.Path == "/Prestador"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        geoIpServiceMock.VerifyAll();
    }
}
