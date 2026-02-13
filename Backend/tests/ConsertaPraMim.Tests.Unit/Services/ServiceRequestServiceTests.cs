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
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IZipGeocodingService> _zipGeocodingServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly ServiceRequestService _service;

    public ServiceRequestServiceTests()
    {
        _requestRepoMock = new Mock<IServiceRequestRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _zipGeocodingServiceMock = new Mock<IZipGeocodingService>();
        _notificationServiceMock = new Mock<INotificationService>();

        _userRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());
        _service = new ServiceRequestService(
            _requestRepoMock.Object,
            _userRepoMock.Object,
            _zipGeocodingServiceMock.Object,
            _notificationServiceMock.Object);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnGuid_WhenSuccess()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var dto = new CreateServiceRequestDto(ServiceCategory.Electrical, "Fix my lamp", "Street", "City", "123", -23.5, -46.6);
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
        var request = new ServiceRequest { Id = id, Description = "Test", Status = ServiceRequestStatus.Created, Category = ServiceCategory.Other };
        _requestRepoMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(request);

        // Act
        var result = await _service.GetByIdAsync(id);

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
        var result = await _service.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }
}
