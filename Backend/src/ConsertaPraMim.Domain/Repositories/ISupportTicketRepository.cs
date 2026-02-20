using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Repositories;

public interface ISupportTicketRepository
{
    Task AddAsync(SupportTicket ticket);
    Task UpdateAsync(SupportTicket ticket);
    Task<SupportTicket?> GetProviderTicketByIdWithMessagesAsync(Guid providerId, Guid ticketId);
    Task<SupportTicket?> GetAdminTicketByIdWithMessagesAsync(Guid ticketId);
    Task<(IReadOnlyList<SupportTicket> Items, int TotalCount)> GetProviderTicketsAsync(
        Guid providerId,
        SupportTicketStatus? status,
        SupportTicketPriority? priority,
        string? search,
        int page,
        int pageSize);
    Task<(IReadOnlyList<SupportTicket> Items, int TotalCount)> GetAdminTicketsAsync(
        SupportTicketStatus? status,
        SupportTicketPriority? priority,
        Guid? assignedAdminUserId,
        bool? assignedOnly,
        string? search,
        string? sortBy,
        bool sortDescending,
        int page,
        int pageSize);
    Task<SupportTicketAdminQueueIndicators> GetAdminQueueIndicatorsAsync(
        SupportTicketStatus? status,
        SupportTicketPriority? priority,
        Guid? assignedAdminUserId,
        bool? assignedOnly,
        string? search,
        DateTime asOfUtc,
        int firstResponseSlaMinutes);
}

public record SupportTicketAdminQueueIndicators(
    int OpenCount,
    int InProgressCount,
    int WaitingProviderCount,
    int ResolvedCount,
    int ClosedCount,
    int WithoutFirstAdminResponseCount,
    int OverdueWithoutFirstResponseCount,
    int UnassignedCount);
