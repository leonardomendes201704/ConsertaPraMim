using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Repositories;

public interface ISupportTicketRepository
{
    Task AddAsync(SupportTicket ticket);
    Task UpdateAsync(SupportTicket ticket);
    Task<SupportTicket?> GetProviderTicketByIdWithMessagesAsync(Guid providerId, Guid ticketId);
    Task<(IReadOnlyList<SupportTicket> Items, int TotalCount)> GetProviderTicketsAsync(
        Guid providerId,
        SupportTicketStatus? status,
        SupportTicketPriority? priority,
        string? search,
        int page,
        int pageSize);
}
