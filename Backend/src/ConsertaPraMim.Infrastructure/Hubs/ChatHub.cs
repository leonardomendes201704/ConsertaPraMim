using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ConsertaPraMim.Infrastructure.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;

    public ChatHub(IChatService chatService)
    {
        _chatService = chatService;
    }

    public async Task<bool> JoinPersonalGroup()
    {
        if (!TryGetCurrentUser(out var userGuid, out _)) return false;
        await Groups.AddToGroupAsync(Context.ConnectionId, BuildUserGroup(userGuid));
        return true;
    }

    public async Task<bool> JoinRequestChat(string requestId, string providerId)
    {
        if (!Guid.TryParse(requestId, out var requestGuid)) return false;
        if (!Guid.TryParse(providerId, out var providerGuid)) return false;
        if (!TryGetCurrentUser(out var userGuid, out var role)) return false;

        var allowed = await _chatService.CanAccessConversationAsync(requestGuid, providerGuid, userGuid, role);
        if (!allowed) return false;

        await Groups.AddToGroupAsync(Context.ConnectionId, BuildConversationGroup(requestGuid, providerGuid));
        return true;
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetHistory(string requestId, string providerId)
    {
        if (!Guid.TryParse(requestId, out var requestGuid)) return Array.Empty<ChatMessageDto>();
        if (!Guid.TryParse(providerId, out var providerGuid)) return Array.Empty<ChatMessageDto>();
        if (!TryGetCurrentUser(out var userGuid, out var role)) return Array.Empty<ChatMessageDto>();

        return await _chatService.GetConversationHistoryAsync(requestGuid, providerGuid, userGuid, role);
    }

    public async Task SendMessage(
        string requestId,
        string providerId,
        string? text,
        IEnumerable<ChatAttachmentInputDto>? attachments)
    {
        if (!Guid.TryParse(requestId, out var requestGuid)) return;
        if (!Guid.TryParse(providerId, out var providerGuid)) return;
        if (!TryGetCurrentUser(out var userGuid, out var role)) return;

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

    private bool TryGetCurrentUser(out Guid userId, out string role)
    {
        userId = Guid.Empty;
        role = string.Empty;

        var userIdRaw = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdRaw, out userId))
        {
            return false;
        }

        role = Context.User?.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        return !string.IsNullOrWhiteSpace(role);
    }
}
