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
    IReadOnlyList<AdminStatusCountDto>? PaymentFailuresByChannel = null);
