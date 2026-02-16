using Moq;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Enums;
using Microsoft.Extensions.Configuration;
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
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Reviews:EvaluationWindowDays"] = "30"
            })
            .Build();

        _service = new ReviewService(
            _reviewRepoMock.Object,
            _requestRepoMock.Object,
            _userRepoMock.Object,
            configuration);
    }

    [Fact]
    public async Task SubmitClientReviewAsync_ShouldCalculateAverage_WhenSuccess()
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
            Proposals = new List<Proposal> { new Proposal { ProviderId = providerId, Accepted = true } },
            PaymentTransactions = new List<ServicePaymentTransaction>
            {
                new() { Status = PaymentTransactionStatus.Paid }
            }
        };

        var provider = new User 
        { 
            Id = providerId, 
            ProviderProfile = new ProviderProfile { Rating = 4.0, ReviewCount = 1 } 
        };

        _requestRepoMock.Setup(r => r.GetByIdAsync(requestId)).ReturnsAsync(request);
        _reviewRepoMock.Setup(r => r.GetByRequestAndReviewerAsync(requestId, clientId)).ReturnsAsync((Review?)null);
        _userRepoMock.Setup(r => r.GetByIdAsync(providerId)).ReturnsAsync(provider);

        // Act
        // New rating 5.0. Old: (4.0 * 1) = 4.0. New: (4.0 + 5.0) / 2 = 4.5
        var result = await _service.SubmitClientReviewAsync(clientId, new CreateReviewDto(requestId, 5, "Great!"));

        // Assert
        Assert.True(result);
        Assert.Equal(4.5, provider.ProviderProfile.Rating);
        Assert.Equal(2, provider.ProviderProfile.ReviewCount);
        _reviewRepoMock.Verify(r => r.AddAsync(It.Is<Review>(review =>
            review.RequestId == requestId &&
            review.ClientId == clientId &&
            review.ProviderId == providerId &&
            review.ReviewerUserId == clientId &&
            review.ReviewerRole == UserRole.Client &&
            review.RevieweeUserId == providerId &&
            review.RevieweeRole == UserRole.Provider &&
            review.Rating == 5 &&
            review.Comment == "Great!")), Times.Once);
        _userRepoMock.Verify(r => r.UpdateAsync(provider), Times.Once);
    }

    [Fact]
    public async Task SubmitClientReviewAsync_ShouldReturnFalse_WhenSameReviewerAlreadyReviewedRequest()
    {
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var request = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            Status = ServiceRequestStatus.Completed,
            Proposals = new List<Proposal> { new Proposal { ProviderId = providerId, Accepted = true } },
            PaymentTransactions = new List<ServicePaymentTransaction>
            {
                new() { Status = PaymentTransactionStatus.Paid }
            }
        };

        _requestRepoMock.Setup(r => r.GetByIdAsync(requestId)).ReturnsAsync(request);
        _reviewRepoMock
            .Setup(r => r.GetByRequestAndReviewerAsync(requestId, clientId))
            .ReturnsAsync(new Review
            {
                RequestId = requestId,
                ReviewerUserId = clientId
            });

        var result = await _service.SubmitClientReviewAsync(clientId, new CreateReviewDto(requestId, 4, "ok"));

        Assert.False(result);
        _reviewRepoMock.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Never);
    }

    [Fact]
    public async Task SubmitClientReviewAsync_ShouldReturnFalse_WhenRequestNotCompleted()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var request = new ServiceRequest { Status = ServiceRequestStatus.Created };
        _requestRepoMock.Setup(r => r.GetByIdAsync(requestId)).ReturnsAsync(request);

        // Act
        var result = await _service.SubmitClientReviewAsync(clientId, new CreateReviewDto(requestId, 5, ""));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SubmitProviderReviewAsync_ShouldCreateReview_WhenProviderIsAccepted()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var request = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            Status = ServiceRequestStatus.Completed,
            Proposals = new List<Proposal> { new Proposal { ProviderId = providerId, Accepted = true } },
            PaymentTransactions = new List<ServicePaymentTransaction>
            {
                new() { Status = PaymentTransactionStatus.Paid }
            }
        };

        _requestRepoMock.Setup(r => r.GetByIdAsync(requestId)).ReturnsAsync(request);
        _reviewRepoMock.Setup(r => r.GetByRequestAndReviewerAsync(requestId, providerId)).ReturnsAsync((Review?)null);

        var result = await _service.SubmitProviderReviewAsync(providerId, new CreateReviewDto(requestId, 5, "Cliente colaborou."));

        Assert.True(result);
        _reviewRepoMock.Verify(r => r.AddAsync(It.Is<Review>(review =>
            review.RequestId == requestId &&
            review.ClientId == clientId &&
            review.ProviderId == providerId &&
            review.ReviewerUserId == providerId &&
            review.ReviewerRole == UserRole.Provider &&
            review.RevieweeUserId == clientId &&
            review.RevieweeRole == UserRole.Client &&
            review.Rating == 5 &&
            review.Comment == "Cliente colaborou.")), Times.Once);
        _userRepoMock.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task SubmitProviderReviewAsync_ShouldReturnFalse_WhenProviderIsNotAccepted()
    {
        var providerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var request = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            Status = ServiceRequestStatus.Completed,
            Proposals = new List<Proposal> { new Proposal { ProviderId = Guid.NewGuid(), Accepted = true } },
            PaymentTransactions = new List<ServicePaymentTransaction>
            {
                new() { Status = PaymentTransactionStatus.Paid }
            }
        };

        _requestRepoMock.Setup(r => r.GetByIdAsync(requestId)).ReturnsAsync(request);

        var result = await _service.SubmitProviderReviewAsync(providerId, new CreateReviewDto(requestId, 4, "ok"));

        Assert.False(result);
        _reviewRepoMock.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Never);
    }

    [Fact]
    public async Task SubmitClientReviewAsync_ShouldReturnFalse_WhenRequestIsUnpaid()
    {
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var request = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            Status = ServiceRequestStatus.Completed,
            Proposals = new List<Proposal> { new Proposal { ProviderId = providerId, Accepted = true } },
            PaymentTransactions = new List<ServicePaymentTransaction>
            {
                new() { Status = PaymentTransactionStatus.Pending }
            }
        };

        _requestRepoMock.Setup(r => r.GetByIdAsync(requestId)).ReturnsAsync(request);

        var result = await _service.SubmitClientReviewAsync(clientId, new CreateReviewDto(requestId, 5, "ok"));

        Assert.False(result);
        _reviewRepoMock.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Never);
    }

    [Fact]
    public async Task SubmitClientReviewAsync_ShouldReturnFalse_WhenReviewWindowIsExpired()
    {
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var request = new ServiceRequest
        {
            Id = requestId,
            ClientId = clientId,
            Status = ServiceRequestStatus.Completed,
            UpdatedAt = DateTime.UtcNow.AddDays(-31),
            Proposals = new List<Proposal> { new Proposal { ProviderId = providerId, Accepted = true } },
            PaymentTransactions = new List<ServicePaymentTransaction>
            {
                new() { Status = PaymentTransactionStatus.Paid }
            }
        };

        _requestRepoMock.Setup(r => r.GetByIdAsync(requestId)).ReturnsAsync(request);

        var result = await _service.SubmitClientReviewAsync(clientId, new CreateReviewDto(requestId, 4, "ok"));

        Assert.False(result);
        _reviewRepoMock.Verify(r => r.AddAsync(It.IsAny<Review>()), Times.Never);
    }

    [Fact]
    public async Task GetProviderScoreSummaryAsync_ShouldReturnAverageAndDistribution()
    {
        var providerId = Guid.NewGuid();
        _reviewRepoMock
            .Setup(r => r.GetByRevieweeAsync(providerId, UserRole.Provider))
            .ReturnsAsync(new List<Review>
            {
                new() { Rating = 5, RevieweeUserId = providerId, RevieweeRole = UserRole.Provider },
                new() { Rating = 4, RevieweeUserId = providerId, RevieweeRole = UserRole.Provider },
                new() { Rating = 4, RevieweeUserId = providerId, RevieweeRole = UserRole.Provider },
                new() { Rating = 2, RevieweeUserId = providerId, RevieweeRole = UserRole.Provider }
            });

        var summary = await _service.GetProviderScoreSummaryAsync(providerId);

        Assert.Equal(providerId, summary.UserId);
        Assert.Equal(UserRole.Provider, summary.UserRole);
        Assert.Equal(3.75, summary.AverageRating);
        Assert.Equal(4, summary.TotalReviews);
        Assert.Equal(1, summary.FiveStarCount);
        Assert.Equal(2, summary.FourStarCount);
        Assert.Equal(0, summary.ThreeStarCount);
        Assert.Equal(1, summary.TwoStarCount);
        Assert.Equal(0, summary.OneStarCount);
    }

    [Fact]
    public async Task GetClientScoreSummaryAsync_ShouldReturnZeroSummary_WhenNoReviews()
    {
        var clientId = Guid.NewGuid();
        _reviewRepoMock
            .Setup(r => r.GetByRevieweeAsync(clientId, UserRole.Client))
            .ReturnsAsync(new List<Review>());

        var summary = await _service.GetClientScoreSummaryAsync(clientId);

        Assert.Equal(clientId, summary.UserId);
        Assert.Equal(UserRole.Client, summary.UserRole);
        Assert.Equal(0, summary.AverageRating);
        Assert.Equal(0, summary.TotalReviews);
        Assert.Equal(0, summary.FiveStarCount);
        Assert.Equal(0, summary.FourStarCount);
        Assert.Equal(0, summary.ThreeStarCount);
        Assert.Equal(0, summary.TwoStarCount);
        Assert.Equal(0, summary.OneStarCount);
    }

    [Fact]
    public async Task ReportReviewAsync_ShouldSetReported_WhenActorCanReport()
    {
        var reviewId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        var review = new Review
        {
            Id = reviewId,
            ClientId = clientId,
            ProviderId = providerId,
            ReviewerUserId = clientId,
            ModerationStatus = ReviewModerationStatus.None
        };

        _reviewRepoMock.Setup(r => r.GetByIdAsync(reviewId)).ReturnsAsync(review);

        var result = await _service.ReportReviewAsync(
            reviewId,
            providerId,
            UserRole.Provider,
            new ReportReviewDto("Comentario ofensivo"));

        Assert.True(result);
        Assert.Equal(ReviewModerationStatus.Reported, review.ModerationStatus);
        Assert.Equal(providerId, review.ReportedByUserId);
        Assert.Equal("Comentario ofensivo", review.ReportReason);
        Assert.NotNull(review.ReportedAtUtc);
        _reviewRepoMock.Verify(r => r.UpdateAsync(review), Times.Once);
    }

    [Fact]
    public async Task ReportReviewAsync_ShouldReturnFalse_WhenReporterIsAuthor()
    {
        var reviewId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var review = new Review
        {
            Id = reviewId,
            ClientId = authorId,
            ProviderId = Guid.NewGuid(),
            ReviewerUserId = authorId,
            ModerationStatus = ReviewModerationStatus.None
        };

        _reviewRepoMock.Setup(r => r.GetByIdAsync(reviewId)).ReturnsAsync(review);

        var result = await _service.ReportReviewAsync(
            reviewId,
            authorId,
            UserRole.Client,
            new ReportReviewDto("Nao gostei"));

        Assert.False(result);
        _reviewRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Review>()), Times.Never);
    }

    [Fact]
    public async Task ModerateReviewAsync_ShouldHideComment_WhenDecisionIsHideComment()
    {
        var reviewId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var review = new Review
        {
            Id = reviewId,
            ModerationStatus = ReviewModerationStatus.Reported
        };

        _reviewRepoMock.Setup(r => r.GetByIdAsync(reviewId)).ReturnsAsync(review);

        var result = await _service.ModerateReviewAsync(
            reviewId,
            adminId,
            new ModerateReviewDto("HideComment", "Abuso confirmado"));

        Assert.True(result);
        Assert.Equal(ReviewModerationStatus.Hidden, review.ModerationStatus);
        Assert.Equal(adminId, review.ModeratedByAdminId);
        Assert.Equal("Abuso confirmado", review.ModerationReason);
        Assert.NotNull(review.ModeratedAtUtc);
        _reviewRepoMock.Verify(r => r.UpdateAsync(review), Times.Once);
    }
}
