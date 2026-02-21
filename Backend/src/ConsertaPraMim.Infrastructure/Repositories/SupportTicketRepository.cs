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

            var persistedAttachmentIds = await _context.SupportTicketMessageAttachments
                .AsNoTracking()
                .Where(a => persistedMessageIdSet.Contains(a.SupportTicketMessageId))
                .Select(a => a.Id)
                .ToListAsync();
            var persistedAttachmentIdSet = persistedAttachmentIds.ToHashSet();

            foreach (var message in ticket.Messages ?? Array.Empty<SupportTicketMessage>())
            {
                if (persistedMessageIdSet.Contains(message.Id))
                {
                    foreach (var attachment in message.Attachments ?? Array.Empty<SupportTicketMessageAttachment>())
                    {
                        if (persistedAttachmentIdSet.Contains(attachment.Id))
                        {
                            continue;
                        }

                        _context.Entry(attachment).State = EntityState.Added;
                    }
                }
                else
                {
                    _context.Entry(message).State = EntityState.Added;
                }
            }

            await _context.SaveChangesAsync();
            return;
        }

        var persisted = await _context.SupportTickets
            .Include(t => t.Messages)
                .ThenInclude(m => m.Attachments)
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

            var incomingAttachments = (incomingMessage.Attachments ?? Array.Empty<SupportTicketMessageAttachment>())
                .ToDictionary(a => a.Id, a => a);

            foreach (var existingAttachment in existingMessage.Attachments.ToList())
            {
                if (!incomingAttachments.TryGetValue(existingAttachment.Id, out var incomingAttachment))
                {
                    _context.SupportTicketMessageAttachments.Remove(existingAttachment);
                    continue;
                }

                _context.Entry(existingAttachment).CurrentValues.SetValues(incomingAttachment);
            }

            var existingAttachmentIds = existingMessage.Attachments.Select(a => a.Id).ToHashSet();
            foreach (var incomingAttachment in incomingAttachments.Values)
            {
                if (existingAttachmentIds.Contains(incomingAttachment.Id))
                {
                    continue;
                }

                existingMessage.Attachments.Add(incomingAttachment);
            }
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
            .Include(t => t.Messages)
                .ThenInclude(m => m.Attachments)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.ProviderId == providerId);
    }

    public async Task<SupportTicket?> GetAdminTicketByIdWithMessagesAsync(Guid ticketId)
    {
        return await _context.SupportTickets
            .Include(t => t.Provider)
            .Include(t => t.AssignedAdminUser)
            .Include(t => t.Messages)
                .ThenInclude(m => m.AuthorUser)
            .Include(t => t.Messages)
                .ThenInclude(m => m.Attachments)
            .FirstOrDefaultAsync(t => t.Id == ticketId);
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

    public async Task<(IReadOnlyList<SupportTicket> Items, int TotalCount)> GetAdminTicketsAsync(
        SupportTicketStatus? status,
        SupportTicketPriority? priority,
        Guid? assignedAdminUserId,
        bool? assignedOnly,
        string? search,
        string? sortBy,
        bool sortDescending,
        int page,
        int pageSize)
    {
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

        var query = _context.SupportTickets
            .AsNoTracking()
            .Include(t => t.Provider)
            .Include(t => t.AssignedAdminUser)
            .Include(t => t.Messages)
                .ThenInclude(m => m.AuthorUser)
            .AsQueryable();

        query = ApplyAdminFilters(
            query,
            status,
            priority,
            assignedAdminUserId,
            assignedOnly,
            search);

        var totalCount = await query.CountAsync();
        query = ApplyAdminSort(query, sortBy, sortDescending);

        var items = await query
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<SupportTicketAdminQueueIndicators> GetAdminQueueIndicatorsAsync(
        SupportTicketStatus? status,
        SupportTicketPriority? priority,
        Guid? assignedAdminUserId,
        bool? assignedOnly,
        string? search,
        DateTime asOfUtc,
        int firstResponseSlaMinutes)
    {
        var activeStatuses = new[]
        {
            SupportTicketStatus.Open,
            SupportTicketStatus.InProgress,
            SupportTicketStatus.WaitingProvider
        };
        var normalizedAsOfUtc = asOfUtc.Kind == DateTimeKind.Utc
            ? asOfUtc
            : asOfUtc.ToUniversalTime();
        var normalizedFirstResponseSlaMinutes = firstResponseSlaMinutes <= 0
            ? 60
            : Math.Min(firstResponseSlaMinutes, 24 * 60 * 7);
        var firstResponseCutoffUtc = normalizedAsOfUtc.AddMinutes(-normalizedFirstResponseSlaMinutes);

        var query = _context.SupportTickets
            .AsNoTracking()
            .AsQueryable();

        query = ApplyAdminFilters(
            query,
            status,
            priority,
            assignedAdminUserId,
            assignedOnly,
            search);

        var openCount = await query.CountAsync(t => t.Status == SupportTicketStatus.Open);
        var inProgressCount = await query.CountAsync(t => t.Status == SupportTicketStatus.InProgress);
        var waitingProviderCount = await query.CountAsync(t => t.Status == SupportTicketStatus.WaitingProvider);
        var resolvedCount = await query.CountAsync(t => t.Status == SupportTicketStatus.Resolved);
        var closedCount = await query.CountAsync(t => t.Status == SupportTicketStatus.Closed);

        var withoutFirstAdminResponseCount = await query.CountAsync(t =>
            activeStatuses.Contains(t.Status) &&
            !t.FirstAdminResponseAtUtc.HasValue);
        var overdueWithoutFirstResponseCount = await query.CountAsync(t =>
            activeStatuses.Contains(t.Status) &&
            !t.FirstAdminResponseAtUtc.HasValue &&
            t.OpenedAtUtc <= firstResponseCutoffUtc);
        var unassignedCount = await query.CountAsync(t =>
            activeStatuses.Contains(t.Status) &&
            !t.AssignedAdminUserId.HasValue);

        return new SupportTicketAdminQueueIndicators(
            openCount,
            inProgressCount,
            waitingProviderCount,
            resolvedCount,
            closedCount,
            withoutFirstAdminResponseCount,
            overdueWithoutFirstResponseCount,
            unassignedCount);
    }

    private static IQueryable<SupportTicket> ApplyAdminFilters(
        IQueryable<SupportTicket> query,
        SupportTicketStatus? status,
        SupportTicketPriority? priority,
        Guid? assignedAdminUserId,
        bool? assignedOnly,
        string? search)
    {
        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        if (priority.HasValue)
        {
            query = query.Where(t => t.Priority == priority.Value);
        }

        if (assignedOnly.HasValue)
        {
            query = assignedOnly.Value
                ? query.Where(t => t.AssignedAdminUserId.HasValue)
                : query.Where(t => !t.AssignedAdminUserId.HasValue);
        }

        if (assignedAdminUserId.HasValue && assignedAdminUserId.Value != Guid.Empty)
        {
            query = query.Where(t => t.AssignedAdminUserId == assignedAdminUserId.Value);
        }

        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (normalizedSearch != null)
        {
            query = query.Where(t =>
                t.Subject.Contains(normalizedSearch) ||
                t.Category.Contains(normalizedSearch) ||
                t.Provider.Name.Contains(normalizedSearch) ||
                t.Provider.Email.Contains(normalizedSearch));
        }

        return query;
    }

    private static IQueryable<SupportTicket> ApplyAdminSort(
        IQueryable<SupportTicket> query,
        string? sortBy,
        bool sortDescending)
    {
        var normalizedSort = string.IsNullOrWhiteSpace(sortBy)
            ? "lastinteraction"
            : sortBy.Trim().ToLowerInvariant();

        return normalizedSort switch
        {
            "openedat" or "openedatutc" => sortDescending
                ? query.OrderByDescending(t => t.OpenedAtUtc).ThenByDescending(t => t.LastInteractionAtUtc)
                : query.OrderBy(t => t.OpenedAtUtc).ThenBy(t => t.LastInteractionAtUtc),
            "priority" => sortDescending
                ? query.OrderByDescending(t => t.Priority).ThenByDescending(t => t.LastInteractionAtUtc)
                : query.OrderBy(t => t.Priority).ThenByDescending(t => t.LastInteractionAtUtc),
            "status" => sortDescending
                ? query.OrderByDescending(t => t.Status).ThenByDescending(t => t.LastInteractionAtUtc)
                : query.OrderBy(t => t.Status).ThenByDescending(t => t.LastInteractionAtUtc),
            "subject" => sortDescending
                ? query.OrderByDescending(t => t.Subject).ThenByDescending(t => t.LastInteractionAtUtc)
                : query.OrderBy(t => t.Subject).ThenByDescending(t => t.LastInteractionAtUtc),
            "provider" or "providername" => sortDescending
                ? query.OrderByDescending(t => t.Provider.Name).ThenByDescending(t => t.LastInteractionAtUtc)
                : query.OrderBy(t => t.Provider.Name).ThenByDescending(t => t.LastInteractionAtUtc),
            "assignedadmin" or "assignedadminname" => sortDescending
                ? query.OrderByDescending(t => t.AssignedAdminUser!.Name).ThenByDescending(t => t.LastInteractionAtUtc)
                : query.OrderBy(t => t.AssignedAdminUser!.Name).ThenByDescending(t => t.LastInteractionAtUtc),
            "firstadminresponse" or "firstadminresponseatutc" => sortDescending
                ? query.OrderByDescending(t => t.FirstAdminResponseAtUtc).ThenByDescending(t => t.LastInteractionAtUtc)
                : query.OrderBy(t => t.FirstAdminResponseAtUtc).ThenByDescending(t => t.LastInteractionAtUtc),
            _ => sortDescending
                ? query.OrderByDescending(t => t.LastInteractionAtUtc).ThenByDescending(t => t.OpenedAtUtc)
                : query.OrderBy(t => t.LastInteractionAtUtc).ThenBy(t => t.OpenedAtUtc)
        };
    }
}
