using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IChatService
{
    Task<bool> CanAccessConversationAsync(Guid requestId, Guid providerId, Guid userId, string role);
    Task<IReadOnlyList<ChatMessageDto>> GetConversationHistoryAsync(Guid requestId, Guid providerId, Guid userId, string role);
    Task<Guid?> ResolveRecipientIdAsync(Guid requestId, Guid providerId, Guid senderId);
    Task<ChatMessageDto?> SendMessageAsync(
        Guid requestId,
        Guid providerId,
        Guid senderId,
        string role,
        string? text,
        IEnumerable<ChatAttachmentInputDto>? attachments);
}
