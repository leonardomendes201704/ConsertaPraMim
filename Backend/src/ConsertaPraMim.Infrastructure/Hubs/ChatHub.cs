using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace ConsertaPraMim.Infrastructure.Hubs;

public class ChatHub : Hub
{
    private readonly IChatService _chatService;

    public ChatHub(IChatService chatService)
    {
        _chatService = chatService;
    }

    public async Task<bool> JoinPersonalGroup(string userId)
    {
        if (!Guid.TryParse(userId, out var userGuid)) return false;
        await Groups.AddToGroupAsync(Context.ConnectionId, BuildUserGroup(userGuid));
        return true;
    }

    public async Task<bool> JoinRequestChat(string requestId, string providerId, string userId, string role)
    {
        if (!Guid.TryParse(requestId, out var requestGuid)) return false;
        if (!Guid.TryParse(providerId, out var providerGuid)) return false;
        if (!Guid.TryParse(userId, out var userGuid)) return false;

        var allowed = await _chatService.CanAccessConversationAsync(requestGuid, providerGuid, userGuid, role);
        if (!allowed) return false;

        await Groups.AddToGroupAsync(Context.ConnectionId, BuildConversationGroup(requestGuid, providerGuid));
        return true;
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetHistory(string requestId, string providerId, string userId, string role)
    {
        if (!Guid.TryParse(requestId, out var requestGuid)) return Array.Empty<ChatMessageDto>();
        if (!Guid.TryParse(providerId, out var providerGuid)) return Array.Empty<ChatMessageDto>();
        if (!Guid.TryParse(userId, out var userGuid)) return Array.Empty<ChatMessageDto>();

        return await _chatService.GetConversationHistoryAsync(requestGuid, providerGuid, userGuid, role);
    }

    public async Task SendMessage(
        string requestId,
        string providerId,
        string userId,
        string role,
        string? text,
        IEnumerable<ChatAttachmentInputDto>? attachments)
    {
        if (!Guid.TryParse(requestId, out var requestGuid)) return;
        if (!Guid.TryParse(providerId, out var providerGuid)) return;
        if (!Guid.TryParse(userId, out var userGuid)) return;

        var savedMessage = await _chatService.SendMessageAsync(
            requestGuid,
            providerGuid,
            userGuid,
            role,
            text,
            attachments);

        if (savedMessage == null) return;

        await Clients.Group(BuildConversationGroup(requestGuid, providerGuid))
            .SendAsync("ReceiveChatMessage", savedMessage);

        var recipientId = await _chatService.ResolveRecipientIdAsync(requestGuid, providerGuid, userGuid);
        if (recipientId.HasValue && recipientId.Value != userGuid)
        {
            await Clients.Group(BuildUserGroup(recipientId.Value))
                .SendAsync("ReceiveChatMessage", savedMessage);
        }
    }

    private static string BuildConversationGroup(Guid requestId, Guid providerId)
    {
        return $"chat:{requestId:N}:{providerId:N}";
    }

    private static string BuildUserGroup(Guid userId)
    {
        return $"chat-user:{userId:N}";
    }
}
