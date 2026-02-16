using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public record CreateReviewDto(Guid RequestId, int Rating, string Comment);

public record ReportReviewDto(string Reason);

public record ModerateReviewDto(string Decision, string? Reason);

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
    DateTime CreatedAt,
    bool IsReported = false,
    string? ModerationStatus = null,
    string? ReportReason = null,
    Guid? ReportedByUserId = null,
    DateTime? ReportedAtUtc = null,
    Guid? ModeratedByAdminId = null,
    string? ModerationReason = null,
    DateTime? ModeratedAtUtc = null);

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
