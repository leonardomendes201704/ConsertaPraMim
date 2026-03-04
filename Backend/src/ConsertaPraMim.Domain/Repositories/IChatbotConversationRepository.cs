using ConsertaPraMim.Domain.Entities;

namespace ConsertaPraMim.Domain.Repositories;

public interface IChatbotConversationRepository
{
    Task<ChatbotConversation?> GetByIdAsync(Guid conversationId);
    Task<ChatbotConversation?> GetByIdForUpdateAsync(Guid conversationId);
    Task<ChatbotConversation?> GetByClientAndChannelAsync(Guid clientId, string channel, string channelConversationId);
    Task AddConversationAsync(ChatbotConversation conversation);
    Task UpdateConversationAsync(ChatbotConversation conversation);
    Task AddMessageAsync(ChatbotMessage message);
    Task AddContextSnapshotAsync(ChatbotContextSnapshot snapshot);
    Task AddActionLogAsync(ChatbotActionLog actionLog);
    Task<IReadOnlyList<ChatbotMessage>> GetMessagesAsync(Guid conversationId, int take);
    Task<IReadOnlyList<ChatbotContextSnapshot>> GetContextSnapshotsAsync(Guid conversationId, int take);
    Task<IReadOnlyList<ChatbotActionLog>> GetActionLogsAsync(Guid conversationId, int take);
}
