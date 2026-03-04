using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class TelegramChatbotSchedulingServiceTests
{
    [Fact(DisplayName = "Telegram scheduling | Matching | Deve listar prestadores elegiveis ordenados por distancia")]
    public async Task GetEligibleProvidersAsync_ShouldReturnEligibleProvidersOrderedByDistance()
    {
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var requestRepositoryMock = new Mock<IServiceRequestRepository>();
        requestRepositoryMock
            .Setup(repository => repository.GetByIdAsync(requestId))
            .ReturnsAsync(new ServiceRequest
            {
                Id = requestId,
                ClientId = clientId,
                Status = ServiceRequestStatus.Created,
                Category = ServiceCategory.Appliances,
                Latitude = -23.5505,
                Longitude = -46.6333,
                Client = new User
                {
                    Id = clientId,
                    ClientProfileType = ClientProfileType.Pf
                }
            });

        var nearProvider = BuildProvider(
            name: "Prestador Perto",
            latitude: -23.5510,
            longitude: -46.6330,
            radiusKm: 8,
            categories: [ServiceCategory.Appliances],
            rating: 4.9,
            reviewCount: 34);

        var farProvider = BuildProvider(
            name: "Prestador Longe",
            latitude: -23.6200,
            longitude: -46.7000,
            radiusKm: 20,
            categories: [ServiceCategory.Appliances],
            rating: 4.2,
            reviewCount: 12);

        var wrongCategoryProvider = BuildProvider(
            name: "Categoria Errada",
            latitude: -23.5510,
            longitude: -46.6330,
            radiusKm: 8,
            categories: [ServiceCategory.Electrical],
            rating: 5.0,
            reviewCount: 100);

        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock
            .Setup(repository => repository.GetAllAsync())
            .ReturnsAsync([nearProvider, farProvider, wrongCategoryProvider]);

        var service = new TelegramChatbotSchedulingService(
            requestRepositoryMock.Object,
            userRepositoryMock.Object);

        var result = await service.GetEligibleProvidersAsync(clientId, requestId, take: 5);

        Assert.True(result.Success);
        Assert.Equal(2, result.Providers.Count);
        Assert.Equal("Prestador Perto", result.Providers[0].ProviderName);
        Assert.Equal("Prestador Longe", result.Providers[1].ProviderName);
    }

    [Fact(DisplayName = "Telegram scheduling | Matching | Deve bloquear cliente sem acesso ao pedido")]
    public async Task GetEligibleProvidersAsync_ShouldReturnForbidden_WhenClientDoesNotOwnRequest()
    {
        var requestId = Guid.NewGuid();
        var requestRepositoryMock = new Mock<IServiceRequestRepository>();
        requestRepositoryMock
            .Setup(repository => repository.GetByIdAsync(requestId))
            .ReturnsAsync(new ServiceRequest
            {
                Id = requestId,
                ClientId = Guid.NewGuid(),
                Status = ServiceRequestStatus.Created
            });

        var userRepositoryMock = new Mock<IUserRepository>();
        var service = new TelegramChatbotSchedulingService(
            requestRepositoryMock.Object,
            userRepositoryMock.Object);

        var result = await service.GetEligibleProvidersAsync(Guid.NewGuid(), requestId, take: 5);

        Assert.False(result.Success);
        Assert.Equal("forbidden", result.ErrorCode);
    }

    [Fact(DisplayName = "Telegram scheduling | Matching | Deve retornar erro para pedido encerrado")]
    public async Task GetEligibleProvidersAsync_ShouldReturnRequestClosed_WhenRequestIsClosed()
    {
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var requestRepositoryMock = new Mock<IServiceRequestRepository>();
        requestRepositoryMock
            .Setup(repository => repository.GetByIdAsync(requestId))
            .ReturnsAsync(new ServiceRequest
            {
                Id = requestId,
                ClientId = clientId,
                Status = ServiceRequestStatus.Completed
            });

        var userRepositoryMock = new Mock<IUserRepository>();
        var service = new TelegramChatbotSchedulingService(
            requestRepositoryMock.Object,
            userRepositoryMock.Object);

        var result = await service.GetEligibleProvidersAsync(clientId, requestId, take: 5);

        Assert.False(result.Success);
        Assert.Equal("request_closed", result.ErrorCode);
    }

    private static User BuildProvider(
        string name,
        double latitude,
        double longitude,
        double radiusKm,
        IReadOnlyList<ServiceCategory> categories,
        double rating,
        int reviewCount)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            Role = UserRole.Provider,
            IsActive = true,
            ProviderProfile = new ProviderProfile
            {
                BaseLatitude = latitude,
                BaseLongitude = longitude,
                RadiusKm = radiusKm,
                Categories = categories.ToList(),
                Rating = rating,
                ReviewCount = reviewCount,
                ClientPreference = ProviderClientPreference.Both
            }
        };
    }
}
