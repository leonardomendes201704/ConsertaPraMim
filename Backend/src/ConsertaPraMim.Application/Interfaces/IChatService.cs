using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Enums;

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
    Task<IReadOnlyList<ChatMessageReceiptDto>> MarkConversationDeliveredAsync(Guid requestId, Guid providerId, Guid userId, string role);
    Task<IReadOnlyList<ChatMessageReceiptDto>> MarkConversationReadAsync(Guid requestId, Guid providerId, Guid userId, string role);
}
