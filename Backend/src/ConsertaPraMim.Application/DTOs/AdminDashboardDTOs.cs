namespace ConsertaPraMim.Application.DTOs;

public record AdminDashboardQueryDto(
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? EventType,
    string? OperationalStatus,
    string? SearchTerm,
    int Page = 1,
    int PageSize = 20);

public record AdminStatusCountDto(
    string Status,
    int Count);

public record AdminCategoryCountDto(
    string Category,
    int Count);

public record AdminPlanRevenueDto(
    string Plan,
    int Providers,
    decimal UnitMonthlyPrice,
    decimal TotalMonthlyRevenue);

public record AdminPaymentFailureByProviderDto(
    Guid ProviderId,
    string ProviderName,
    int FailedTransactions,
    int AffectedRequests,
    DateTime? LastFailureAtUtc);

public record AdminRecentEventDto(
    string Type,
    Guid ReferenceId,
    DateTime CreatedAt,
    string Title,
    string? Description);

public record AdminReviewRankingItemDto(
    Guid UserId,
    string UserName,
    string UserRole,
    double AverageRating,
    int TotalReviews,
    int FiveStarCount,
    int OneStarCount,
    DateTime? LastReviewAtUtc);

public record AdminReviewOutlierDto(
    Guid UserId,
    string UserName,
    string UserRole,
    double AverageRating,
    int TotalReviews,
    decimal OneStarRatePercent,
    string Reason);

public record AdminDashboardDto(
    int TotalUsers,
    int ActiveUsers,
    int InactiveUsers,
    int TotalProviders,
    int TotalClients,
    int OnlineProviders,
    int OnlineClients,
    int PayingProviders,
    decimal MonthlySubscriptionRevenue,
    IReadOnlyList<AdminPlanRevenueDto> RevenueByPlan,
    int TotalAdmins,
    int TotalRequests,
    int ActiveRequests,
    int RequestsInPeriod,
    IReadOnlyList<AdminStatusCountDto> RequestsByStatus,
    IReadOnlyList<AdminCategoryCountDto> RequestsByCategory,
    int ProposalsInPeriod,
    int AcceptedProposalsInPeriod,
    int ActiveChatConversationsLast24h,
    DateTime FromUtc,
    DateTime ToUtc,
    int Page,
    int PageSize,
    int TotalEvents,
    IReadOnlyList<AdminRecentEventDto> RecentEvents,
    IReadOnlyList<AdminStatusCountDto>? AppointmentsByOperationalStatus = null,
    IReadOnlyList<AdminPaymentFailureByProviderDto>? PaymentFailuresByProvider = null,
    IReadOnlyList<AdminStatusCountDto>? PaymentFailuresByChannel = null,
    IReadOnlyList<AdminReviewRankingItemDto>? ProviderReviewRanking = null,
    IReadOnlyList<AdminReviewRankingItemDto>? ClientReviewRanking = null,
    IReadOnlyList<AdminReviewOutlierDto>? ReviewOutliers = null,
    decimal CreditsGrantedInPeriod = 0m,
    decimal CreditsConsumedInPeriod = 0m,
    decimal CreditsOpenBalance = 0m,
    decimal CreditsExpiringInNext30Days = 0m,
    decimal AppointmentConfirmationInSlaRatePercent = 0m,
    decimal AppointmentRescheduleRatePercent = 0m,
    decimal AppointmentCancellationRatePercent = 0m,
    decimal ReminderFailureRatePercent = 0m,
    int ReminderAttemptsInPeriod = 0,
    int ReminderFailuresInPeriod = 0,
    IReadOnlyList<AdminStatusCountDto>? ProvidersByOperationalStatus = null);

public record AdminCoverageMapProviderDto(
    Guid ProviderId,
    string ProviderName,
    double Latitude,
    double Longitude,
    double RadiusKm,
    string OperationalStatus,
    bool IsActive);

public record AdminCoverageMapRequestDto(
    Guid RequestId,
    string Status,
    string Category,
    string Description,
    string AddressCity,
    string AddressStreet,
    double Latitude,
    double Longitude,
    DateTime CreatedAtUtc);

public record AdminCoverageMapDto(
    IReadOnlyList<AdminCoverageMapProviderDto> Providers,
    IReadOnlyList<AdminCoverageMapRequestDto> Requests,
    DateTime GeneratedAtUtc);
