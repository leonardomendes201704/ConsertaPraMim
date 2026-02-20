using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class SupportTicketRepository : ISupportTicketRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public SupportTicketRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SupportTicket ticket)
    {
        await _context.SupportTickets.AddAsync(ticket);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(SupportTicket ticket)
    {
        var trackedEntry = _context.ChangeTracker
            .Entries<SupportTicket>()
            .FirstOrDefault(e => e.Entity.Id == ticket.Id);
        if (trackedEntry != null)
        {
            var persistedMessageIds = await _context.SupportTicketMessages
                .AsNoTracking()
                .Where(m => m.SupportTicketId == ticket.Id)
                .Select(m => m.Id)
                .ToListAsync();
            var persistedMessageIdSet = persistedMessageIds.ToHashSet();

            foreach (var message in ticket.Messages ?? Array.Empty<SupportTicketMessage>())
            {
                if (persistedMessageIdSet.Contains(message.Id))
                {
                    continue;
                }

                _context.Entry(message).State = EntityState.Added;
            }

            await _context.SaveChangesAsync();
            return;
        }

        var persisted = await _context.SupportTickets
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == ticket.Id);

        if (persisted == null)
        {
            throw new DbUpdateConcurrencyException("Support ticket not found for update.");
        }

        _context.Entry(persisted).CurrentValues.SetValues(ticket);

        var incomingMessages = (ticket.Messages ?? Array.Empty<SupportTicketMessage>())
            .ToDictionary(m => m.Id, m => m);

        foreach (var existingMessage in persisted.Messages.ToList())
        {
            if (!incomingMessages.TryGetValue(existingMessage.Id, out var incomingMessage))
            {
                _context.SupportTicketMessages.Remove(existingMessage);
                continue;
            }

            _context.Entry(existingMessage).CurrentValues.SetValues(incomingMessage);
        }

        var existingMessageIds = persisted.Messages.Select(m => m.Id).ToHashSet();
        foreach (var incomingMessage in incomingMessages.Values)
        {
            if (existingMessageIds.Contains(incomingMessage.Id))
            {
                continue;
            }

            persisted.Messages.Add(incomingMessage);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<SupportTicket?> GetProviderTicketByIdWithMessagesAsync(Guid providerId, Guid ticketId)
    {
        return await _context.SupportTickets
            .Include(t => t.Provider)
            .Include(t => t.AssignedAdminUser)
            .Include(t => t.Messages)
                .ThenInclude(m => m.AuthorUser)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.ProviderId == providerId);
    }

    public async Task<(IReadOnlyList<SupportTicket> Items, int TotalCount)> GetProviderTicketsAsync(
        Guid providerId,
        SupportTicketStatus? status,
        SupportTicketPriority? priority,
        string? search,
        int page,
        int pageSize)
    {
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        var query = _context.SupportTickets
            .AsNoTracking()
            .Include(t => t.Messages)
            .Where(t => t.ProviderId == providerId);

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        if (priority.HasValue)
        {
            query = query.Where(t => t.Priority == priority.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(t =>
                t.Subject.Contains(normalizedSearch) ||
                t.Category.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.LastInteractionAtUtc)
            .ThenByDescending(t => t.OpenedAtUtc)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}
