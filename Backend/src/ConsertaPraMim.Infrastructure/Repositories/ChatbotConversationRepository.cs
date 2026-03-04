using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Infrastructure.Repositories;

public class ChatbotConversationRepository : IChatbotConversationRepository
{
    private readonly ConsertaPraMimDbContext _context;

    public ChatbotConversationRepository(ConsertaPraMimDbContext context)
    {
        _context = context;
    }

    public async Task<ChatbotConversation?> GetByIdAsync(Guid conversationId)
    {
        return await _context.ChatbotConversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId);
    }

    public async Task<ChatbotConversation?> GetByIdForUpdateAsync(Guid conversationId)
    {
        return await _context.ChatbotConversations
            .FirstOrDefaultAsync(c => c.Id == conversationId);
    }

    public async Task<ChatbotConversation?> GetByClientAndChannelAsync(Guid clientId, string channel, string channelConversationId)
    {
        return await _context.ChatbotConversations
            .FirstOrDefaultAsync(c =>
                c.ClientId == clientId &&
                c.Channel == channel &&
                c.ChannelConversationId == channelConversationId);
    }

    public async Task AddConversationAsync(ChatbotConversation conversation)
    {
        await _context.ChatbotConversations.AddAsync(conversation);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateConversationAsync(ChatbotConversation conversation)
    {
        _context.ChatbotConversations.Update(conversation);
        await _context.SaveChangesAsync();
    }

    public async Task AddMessageAsync(ChatbotMessage message)
    {
        await _context.ChatbotMessages.AddAsync(message);
        await _context.SaveChangesAsync();
    }

    public async Task AddContextSnapshotAsync(ChatbotContextSnapshot snapshot)
    {
        await _context.ChatbotContextSnapshots.AddAsync(snapshot);
        await _context.SaveChangesAsync();
    }

    public async Task AddActionLogAsync(ChatbotActionLog actionLog)
    {
        await _context.ChatbotActionLogs.AddAsync(actionLog);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ChatbotMessage>> GetMessagesAsync(Guid conversationId, int take)
    {
        var limit = Math.Clamp(take, 1, 200);
        var messages = await _context.ChatbotMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.SentAtUtc)
            .ThenByDescending(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return messages
            .OrderBy(m => m.SentAtUtc)
            .ThenBy(m => m.CreatedAt)
            .ToList();
    }

    public async Task<IReadOnlyList<ChatbotContextSnapshot>> GetContextSnapshotsAsync(Guid conversationId, int take)
    {
        var limit = Math.Clamp(take, 1, 100);
        var snapshots = await _context.ChatbotContextSnapshots
            .AsNoTracking()
            .Where(s => s.ConversationId == conversationId)
            .OrderByDescending(s => s.CapturedAtUtc)
            .ThenByDescending(s => s.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return snapshots
            .OrderBy(s => s.CapturedAtUtc)
            .ThenBy(s => s.CreatedAt)
            .ToList();
    }

    public async Task<IReadOnlyList<ChatbotActionLog>> GetActionLogsAsync(Guid conversationId, int take)
    {
        var limit = Math.Clamp(take, 1, 100);
        var actionLogs = await _context.ChatbotActionLogs
            .AsNoTracking()
            .Where(a => a.ConversationId == conversationId)
            .OrderByDescending(a => a.OccurredAtUtc)
            .ThenByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return actionLogs
            .OrderBy(a => a.OccurredAtUtc)
            .ThenBy(a => a.CreatedAt)
            .ToList();
    }
}
