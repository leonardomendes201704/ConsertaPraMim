using Moq;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Enums;
using Xunit;

namespace ConsertaPraMim.Tests.Unit.Services;

public class ReviewServiceTests
{
    private readonly Mock<IReviewRepository> _reviewRepoMock;
    private readonly Mock<IServiceRequestRepository> _requestRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly ReviewService _service;

    public ReviewServiceTests()
    {
        _reviewRepoMock = new Mock<IReviewRepository>();
        _requestRepoMock = new Mock<IServiceRequestRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _service = new ReviewService(_reviewRepoMock.Object, _requestRepoMock.Object, _userRepoMock.Object);
    }

    [Fact]
    public async Task SubmitReviewAsync_ShouldCalculateAverage_WhenSuccess()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        
        var request = new ServiceRequest 
        { 
            Id = requestId, 
            ClientId = clientId, 
            Status = ServiceRequestStatus.Completed,
            Proposals = new List<Proposal> { new Proposal { ProviderId = providerId, Accepted = true } }
        };

        var provider = new User 
        { 
            Id = providerId, 
            ProviderProfile = new ProviderProfile { Rating = 4.0, ReviewCount = 1 } 
        };

        _requestRepoMock.Setup(r => r.GetByIdAsync(requestId)).ReturnsAsync(request);
        _reviewRepoMock.Setup(r => r.GetByRequestIdAsync(requestId)).ReturnsAsync((Review?)null);
        _userRepoMock.Setup(r => r.GetByIdAsync(providerId)).ReturnsAsync(provider);

        // Act
        // New rating 5.0. Old: (4.0 * 1) = 4.0. New: (4.0 + 5.0) / 2 = 4.5
        var result = await _service.SubmitReviewAsync(clientId, new CreateReviewDto(requestId, 5, "Great!"));

        // Assert
        Assert.True(result);
        Assert.Equal(4.5, provider.ProviderProfile.Rating);
        Assert.Equal(2, provider.ProviderProfile.ReviewCount);
        _reviewRepoMock.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Once);
        _userRepoMock.Verify(r => r.UpdateAsync(provider), Times.Once);
    }

    [Fact]
    public async Task SubmitReviewAsync_ShouldReturnFalse_WhenRequestNotCompleted()
    {
        // Arrange
        var request = new ServiceRequest { Status = ServiceRequestStatus.Created };
        _requestRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(request);

        // Act
        var result = await _service.SubmitReviewAsync(Guid.NewGuid(), new CreateReviewDto(Guid.NewGuid(), 5, ""));

        // Assert
        Assert.False(result);
    }
}
