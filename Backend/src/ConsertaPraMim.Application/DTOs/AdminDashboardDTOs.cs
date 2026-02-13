namespace ConsertaPraMim.Application.DTOs;

public record AdminDashboardQueryDto(
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? EventType,
    string? SearchTerm,
    int Page = 1,
    int PageSize = 20);

public record AdminStatusCountDto(
    string Status,
    int Count);

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
    int TotalAdmins,
    int TotalRequests,
    int ActiveRequests,
    int RequestsInPeriod,
    IReadOnlyList<AdminStatusCountDto> RequestsByStatus,
    int ProposalsInPeriod,
    int AcceptedProposalsInPeriod,
    int ActiveChatConversationsLast24h,
    DateTime FromUtc,
    DateTime ToUtc,
    int Page,
    int PageSize,
    int TotalEvents,
    IReadOnlyList<AdminRecentEventDto> RecentEvents);
