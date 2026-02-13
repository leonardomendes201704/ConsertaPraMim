using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class ChatMessageRepository : IChatMessageRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public ChatMessageRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ChatMessage message)
    {
        await _context.ChatMessages.AddAsync(message);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ChatMessage>> GetConversationAsync(Guid requestId, Guid providerId)
    {
        return await _context.ChatMessages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Include(m => m.Attachments)
            .Where(m => m.RequestId == requestId && m.ProviderId == providerId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ChatMessage>> GetByPeriodAsync(DateTime? fromUtc, DateTime? toUtc)
    {
        var query = _context.ChatMessages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Include(m => m.Request)
            .ThenInclude(r => r.Client)
            .Include(m => m.Attachments)
            .AsQueryable();

        if (fromUtc.HasValue)
        {
            query = query.Where(m => m.CreatedAt >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(m => m.CreatedAt <= toUtc.Value);
        }

        return await query
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ChatMessage>> GetPendingReceiptsAsync(Guid requestId, Guid providerId, Guid recipientUserId, bool onlyUnread)
    {
        var query = _context.ChatMessages
            .Where(m => m.RequestId == requestId && m.ProviderId == providerId)
            .Where(m => m.SenderId != recipientUserId);

        query = onlyUnread
            ? query.Where(m => m.ReadAt == null)
            : query.Where(m => m.DeliveredAt == null || m.ReadAt == null);

        return await query
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task UpdateRangeAsync(IEnumerable<ChatMessage> messages)
    {
        _context.ChatMessages.UpdateRange(messages);
        await _context.SaveChangesAsync();
    }
}
