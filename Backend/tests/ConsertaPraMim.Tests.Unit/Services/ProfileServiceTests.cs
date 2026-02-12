using Moq;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Enums;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Services;

public class ProfileServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly ProfileService _service;

    public ProfileServiceTests()
    {
        _userRepoMock = new Mock<IUserRepository>();
        _service = new ProfileService(_userRepoMock.Object);
    }

    [Fact]
    public async Task GetProfileAsync_ShouldReturnProfile_WhenUserExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Name = "Test", Email = "a@b.com", Role = UserRole.Client };
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        // Act
        var result = await _service.GetProfileAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Name, result.Name);
    }

    [Fact]
    public async Task UpdateProviderProfileAsync_ShouldUpdate_WhenUserIsProvider()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Role = UserRole.Provider, ProviderProfile = new ProviderProfile() };
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var dto = new UpdateProviderProfileDto(20.0, -23.5, -46.6, new List<ServiceCategory> { ServiceCategory.Masonry });

        // Act
        var result = await _service.UpdateProviderProfileAsync(userId, dto);

        // Assert
        Assert.True(result);
        Assert.Equal(20.0, user.ProviderProfile.RadiusKm);
        _userRepoMock.Verify(r => r.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task UpdateProviderProfileAsync_ShouldReturnFalse_WhenUserIsClient()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Role = UserRole.Client }; // Not a provider
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var dto = new UpdateProviderProfileDto(20.0, 0, 0, new List<ServiceCategory>());

        // Act
        var result = await _service.UpdateProviderProfileAsync(userId, dto);

        // Assert
        Assert.False(result);
        _userRepoMock.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
    }
}
