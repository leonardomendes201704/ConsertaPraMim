using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public record CreateReviewDto(
    Guid RequestId,
    int Rating,
    string Comment,
    int? ServiceQualityRating = null,
    int? PunctualityRating = null,
    int? CommunicationRating = null,
    int? CostBenefitRating = null,
    int? NpsScore = null,
    bool? WouldHireAgain = null);

public record ReviewSubmissionResultDto(
    bool Success,
    string? ErrorCode = null,
    string? ErrorMessage = null);

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
    int? ServiceQualityRating,
    int? PunctualityRating,
    int? CommunicationRating,
    int? CostBenefitRating,
    int? NpsScore,
    bool? WouldHireAgain,
    decimal? CompositeScore,
    DateTime CreatedAt,
    bool IsReported = false,
    string? ModerationStatus = null,
    string? ReportReason = null,
    Guid? ReportedByUserId = null,
    DateTime? ReportedAtUtc = null,
    Guid? ModeratedByAdminId = null,
    string? ModerationReason = null,
    DateTime? ModeratedAtUtc = null);

public record ReviewPendingRequestDto(
    Guid RequestId,
    string CounterpartyName,
    string CounterpartyRole,
    string Category,
    DateTime CompletedAtUtc,
    DateTime ReviewDeadlineUtc,
    int DaysRemaining);

public record ReviewRepurchaseTriggerRequestDto(
    int MinDaysAfterCompletion = 14,
    int MaxDaysAfterCompletion = 90,
    int MaxDispatch = 100,
    bool? RequirePositiveReview = null,
    int MinPositiveRating = 4,
    decimal MinCompositeScore = 70m,
    bool DryRun = false);

public record ReviewRepurchaseTriggerCandidateDto(
    Guid RequestId,
    Guid ClientId,
    string ClientName,
    string Category,
    DateTime CompletedAtUtc,
    int DaysSinceCompletion,
    int? ClientRating,
    decimal? ClientCompositeScore,
    bool? WouldHireAgain);

public record ReviewRepurchaseTriggerResultDto(
    DateTime ExecutedAtUtc,
    int EvaluatedRequests,
    int EligibleCandidates,
    int TriggeredCount,
    int SkippedAlreadyRepurchasedCount,
    int SkippedWithoutPositiveReviewCount,
    int SkippedAlreadyTriggeredCount,
    bool DryRun,
    IReadOnlyList<ReviewRepurchaseTriggerCandidateDto> Candidates);

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
