using Moq;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Enums;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Services;

public class ServiceRequestServiceTests
{
    private readonly Mock<IServiceRequestRepository> _requestRepoMock;
    private readonly Mock<IServiceCategoryRepository> _serviceCategoryRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IZipGeocodingService> _zipGeocodingServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly ServiceRequestService _service;

    public ServiceRequestServiceTests()
    {
        _requestRepoMock = new Mock<IServiceRequestRepository>();
        _serviceCategoryRepoMock = new Mock<IServiceCategoryRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _zipGeocodingServiceMock = new Mock<IZipGeocodingService>();
        _notificationServiceMock = new Mock<INotificationService>();

        _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());
        _userRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid userId) => new User
            {
                Id = userId,
                Role = UserRole.Client,
                IsActive = true
            });
        _serviceCategoryRepoMock
            .Setup(r => r.GetFirstActiveByLegacyAsync(It.IsAny<ServiceCategory>()))
            .ReturnsAsync((ServiceCategory category) => new ServiceCategoryDefinition
            {
                Id = Guid.NewGuid(),
                Name = category.ToPtBr(),
                Slug = category.ToString().ToLowerInvariant(),
                LegacyCategory = category,
                IsActive = true
            });
        _service = new ServiceRequestService(
            _requestRepoMock.Object,
            _serviceCategoryRepoMock.Object,
            _userRepoMock.Object,
            _zipGeocodingServiceMock.Object,
            _notificationServiceMock.Object);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnGuid_WhenSuccess()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var dto = new CreateServiceRequestDto(
            CategoryId: null,
            Category: ServiceCategory.Electrical,
            Description: "Fix my lamp",
            Street: "Street",
            City: "City",
            Zip: "123",
            Lat: -23.5,
            Lng: -46.6);
        _zipGeocodingServiceMock
            .Setup(x => x.ResolveCoordinatesAsync(dto.Zip, dto.Street, dto.City))
            .ReturnsAsync(("123", -23.5, -46.6, dto.Street, dto.City));

        // Act
        var result = await _service.CreateAsync(clientId, dto);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        _requestRepoMock.Verify(r => r.AddAsync(It.Is<ServiceRequest>(req => 
            req.ClientId == clientId && req.Description == dto.Description)), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrow_WhenSelectedCategoryIsInactive()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var dto = new CreateServiceRequestDto(
            CategoryId: categoryId,
            Category: null,
            Description: "Fix my sink",
            Street: "Street",
            City: "City",
            Zip: "11704150",
            Lat: -23.5,
            Lng: -46.6);

        _zipGeocodingServiceMock
            .Setup(x => x.ResolveCoordinatesAsync(dto.Zip, dto.Street, dto.City))
            .ReturnsAsync((dto.Zip, dto.Lat, dto.Lng, dto.Street, dto.City));

        _serviceCategoryRepoMock
            .Setup(r => r.GetByIdAsync(categoryId))
            .ReturnsAsync(new ServiceCategoryDefinition
            {
                Id = categoryId,
                Name = "Eletrica",
                Slug = "eletrica",
                LegacyCategory = ServiceCategory.Electrical,
                IsActive = false
            });

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(clientId, dto));
        _requestRepoMock.Verify(r => r.AddAsync(It.IsAny<ServiceRequest>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowUnauthorized_WhenClientDoesNotExist()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var dto = new CreateServiceRequestDto(
            CategoryId: null,
            Category: ServiceCategory.Electrical,
            Description: "Fix my lamp",
            Street: "Street",
            City: "City",
            Zip: "123",
            Lat: -23.5,
            Lng: -46.6);

        _userRepoMock
            .Setup(r => r.GetByIdAsync(clientId))
            .ReturnsAsync((User?)null);

        // Act + Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CreateAsync(clientId, dto));
        _requestRepoMock.Verify(r => r.AddAsync(It.IsAny<ServiceRequest>()), Times.Never);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnClientRequests_WhenUserIsClient()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requests = new List<ServiceRequest> 
        { 
            new ServiceRequest { Id = Guid.NewGuid(), ClientId = userId, Description = "Req 1", Status = ServiceRequestStatus.Created, Category = ServiceCategory.Plumbing } 
        };
        _requestRepoMock.Setup(r => r.GetByClientIdAsync(userId)).ReturnsAsync(requests);

        // Act
        var result = await _service.GetAllAsync(userId, "Client");

        // Assert
        Assert.Single(result);
        Assert.Equal("Req 1", result.First().Description);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnMatchingRequests_WhenUserIsProviderWithProfile()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var provider = new User 
        { 
            Id = providerId, 
            ProviderProfile = new ProviderProfile 
            { 
                BaseLatitude = -23.0, 
                BaseLongitude = -46.0, 
                RadiusKm = 10, 
                Categories = new List<ServiceCategory> { ServiceCategory.Electrical } 
            } 
        };
        
        var matchingReqs = new List<ServiceRequest> 
        { 
            new ServiceRequest { Id = Guid.NewGuid(), Description = "Matching", Status = ServiceRequestStatus.Created, Category = ServiceCategory.Electrical } 
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(providerId)).ReturnsAsync(provider);
        _requestRepoMock.Setup(r => r.GetMatchingForProviderAsync(
            provider.ProviderProfile.BaseLatitude.Value, 
            provider.ProviderProfile.BaseLongitude.Value, 
            provider.ProviderProfile.RadiusKm, 
            provider.ProviderProfile.Categories,
            null))
            .ReturnsAsync(matchingReqs);

        // Act
        var result = await _service.GetAllAsync(providerId, "Provider");

        // Assert
        Assert.Single(result);
        Assert.Equal("Matching", result.First().Description);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllCreated_WhenProviderHasNoProfile()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var requests = new List<ServiceRequest> 
        { 
            new ServiceRequest { Id = Guid.NewGuid(), Status = ServiceRequestStatus.Created },
            new ServiceRequest { Id = Guid.NewGuid(), Status = ServiceRequestStatus.InProgress }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(providerId)).ReturnsAsync(new User { Id = providerId, ProviderProfile = null });
        _requestRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(requests);

        // Act
        var result = await _service.GetAllAsync(providerId, "Provider");

        // Assert
        Assert.Single(result); // Only 'Created' one
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmpty_WhenProviderHasNoMatchingCategories()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var provider = new User 
        { 
            ProviderProfile = new ProviderProfile { Categories = new List<ServiceCategory> { ServiceCategory.Electrical }, BaseLatitude = 0, BaseLongitude = 0 } 
        };
        
        _userRepoMock.Setup(r => r.GetByIdAsync(providerId)).ReturnsAsync(provider);
        _requestRepoMock.Setup(r => r.GetMatchingForProviderAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), provider.ProviderProfile.Categories, null))
            .ReturnsAsync(new List<ServiceRequest>()); // No matching

        // Act
        var result = await _service.GetAllAsync(providerId, "Provider");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnDto_WhenRequestExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var request = new ServiceRequest
        {
            Id = id,
            ClientId = clientId,
            Description = "Test",
            Status = ServiceRequestStatus.Created,
            Category = ServiceCategory.Other
        };
        _requestRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(request);

        // Act
        var result = await _service.GetByIdAsync(id, clientId, UserRole.Client.ToString());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(id, result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenRequestDoesNotExist()
    {
        // Arrange
        _requestRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((ServiceRequest?)null);

        // Act
        var result = await _service.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid(), UserRole.Client.ToString());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetMapPinsForProviderAsync_ShouldReturnOrderedPins_WithInsideOutsideAndCategoryFlags()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var provider = new User
        {
            Id = providerId,
            ProviderProfile = new ProviderProfile
            {
                BaseLatitude = 0,
                BaseLongitude = 0,
                RadiusKm = 10,
                Categories = new List<ServiceCategory> { ServiceCategory.Electrical }
            }
        };

        var insideAndMatching = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            Category = ServiceCategory.Electrical,
            Description = "Pedido dentro e categoria atendida",
            AddressStreet = "Rua A",
            AddressCity = "Cidade A",
            AddressZip = "11111-111",
            Latitude = 0.03,
            Longitude = 0
        };

        var outsideAndCategoryMiss = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            Category = ServiceCategory.Plumbing,
            Description = "Pedido fora e categoria nao atendida",
            AddressStreet = "Rua B",
            AddressCity = "Cidade B",
            AddressZip = "22222-222",
            Latitude = 0.20,
            Longitude = 0
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(providerId)).ReturnsAsync(provider);
        _requestRepoMock
            .Setup(r => r.GetOpenWithinRadiusAsync(
                0,
                0,
                It.Is<double>(d => Math.Abs(d - 40) < 0.0001)))
            .ReturnsAsync(new List<ServiceRequest> { outsideAndCategoryMiss, insideAndMatching });

        // Act
        var result = (await _service.GetMapPinsForProviderAsync(providerId)).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(insideAndMatching.Id, result[0].RequestId);
        Assert.True(result[0].IsWithinInterestRadius);
        Assert.True(result[0].IsCategoryMatch);

        Assert.Equal(outsideAndCategoryMiss.Id, result[1].RequestId);
        Assert.False(result[1].IsWithinInterestRadius);
        Assert.False(result[1].IsCategoryMatch);
    }

    [Fact]
    public async Task GetMapPinsForProviderAsync_ShouldReturnEmpty_WhenProviderHasNoBaseCoordinates()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.GetByIdAsync(providerId))
            .ReturnsAsync(new User
            {
                Id = providerId,
                ProviderProfile = new ProviderProfile
                {
                    BaseLatitude = null,
                    BaseLongitude = null
                }
            });

        // Act
        var result = await _service.GetMapPinsForProviderAsync(providerId);

        // Assert
        Assert.Empty(result);
        _requestRepoMock.Verify(
            r => r.GetOpenWithinRadiusAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>()),
            Times.Never);
    }

    [Fact]
    public async Task GetMapPinsForProviderAsync_ShouldRespectMaxDistanceAndTake()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var provider = new User
        {
            Id = providerId,
            ProviderProfile = new ProviderProfile
            {
                BaseLatitude = 0,
                BaseLongitude = 0,
                RadiusKm = 10,
                Categories = new List<ServiceCategory> { ServiceCategory.Electrical, ServiceCategory.Plumbing }
            }
        };

        var nearRequest = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            Category = ServiceCategory.Electrical,
            Description = "Pedido perto",
            AddressStreet = "Rua 1",
            AddressCity = "Cidade 1",
            AddressZip = "11111-111",
            Latitude = 0.02,
            Longitude = 0
        };

        var midRequest = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            Category = ServiceCategory.Plumbing,
            Description = "Pedido medio",
            AddressStreet = "Rua 2",
            AddressCity = "Cidade 2",
            AddressZip = "22222-222",
            Latitude = 0.11,
            Longitude = 0
        };

        var farRequest = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            Category = ServiceCategory.Plumbing,
            Description = "Pedido longe",
            AddressStreet = "Rua 3",
            AddressCity = "Cidade 3",
            AddressZip = "33333-333",
            Latitude = 0.22,
            Longitude = 0
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(providerId)).ReturnsAsync(provider);
        _requestRepoMock
            .Setup(r => r.GetOpenWithinRadiusAsync(
                0,
                0,
                It.Is<double>(d => Math.Abs(d - 15) < 0.0001)))
            .ReturnsAsync(new List<ServiceRequest> { farRequest, midRequest, nearRequest });

        // Act
        var result = (await _service.GetMapPinsForProviderAsync(providerId, maxDistanceKm: 15, take: 1)).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(nearRequest.Id, result[0].RequestId);
        Assert.True(result[0].DistanceKm <= 15);
    }
}
