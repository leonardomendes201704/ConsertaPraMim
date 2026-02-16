using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public record CreateReviewDto(Guid RequestId, int Rating, string Comment);

public record ReviewDto(
    Guid Id,
    Guid RequestId,
    Guid ClientId,
    Guid ProviderId,
    Guid ReviewerUserId,
    UserRole ReviewerRole,
    Guid RevieweeUserId,
    UserRole RevieweeRole,
    int Rating,
    string Comment,
    DateTime CreatedAt);

public record ReviewScoreSummaryDto(
    Guid UserId,
    UserRole UserRole,
    double AverageRating,
    int TotalReviews,
    int FiveStarCount,
    int FourStarCount,
    int ThreeStarCount,
    int TwoStarCount,
    int OneStarCount);
