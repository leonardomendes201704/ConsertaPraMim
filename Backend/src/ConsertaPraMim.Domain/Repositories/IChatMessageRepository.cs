using ConsertaPraMim.Domain.Entities;

namespace ConsertaPraMim.Domain.Repositories;

public interface IChatMessageRepository
{
    Task AddAsync(ChatMessage message);
    Task<IReadOnlyList<ChatMessage>> GetConversationAsync(Guid requestId, Guid providerId);
}
