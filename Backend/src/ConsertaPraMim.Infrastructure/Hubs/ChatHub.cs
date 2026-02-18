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
    private readonly IProfileService _profileService;
    private readonly IUserPresenceTracker _userPresenceTracker;
    private readonly IMobilePushNotificationService? _mobilePushNotificationService;

    public ChatHub(
        IChatService chatService,
        IProfileService profileService,
        IUserPresenceTracker userPresenceTracker,
        IMobilePushNotificationService? mobilePushNotificationService = null)
    {
        _chatService = chatService;
        _profileService = profileService;
        _userPresenceTracker = userPresenceTracker;
        _mobilePushNotificationService = mobilePushNotificationService;
    }

    public override async Task OnConnectedAsync()
    {
        if (TryGetCurrentUser(out var userGuid, out _))
        {
            var presenceChanged = _userPresenceTracker.RegisterConnection(Context.ConnectionId, userGuid);
            if (presenceChanged is { IsOnline: true })
            {
                await Clients.All.SendAsync("ReceiveUserPresence", new
                {
                    userId = presenceChanged.UserId,
                    isOnline = presenceChanged.IsOnline,
                    updatedAt = presenceChanged.UpdatedAtUtc
                });
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var presenceChanged = _userPresenceTracker.UnregisterConnection(Context.ConnectionId);
        if (presenceChanged is { IsOnline: false })
        {
            await Clients.All.SendAsync("ReceiveUserPresence", new
            {
                userId = presenceChanged.UserId,
                isOnline = presenceChanged.IsOnline,
                updatedAt = presenceChanged.UpdatedAtUtc
            });
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task<bool> JoinPersonalGroup()
    {
        if (!TryGetCurrentUser(out var userGuid, out _)) return false;
        await Groups.AddToGroupAsync(Context.ConnectionId, BuildUserGroup(userGuid));

        await Clients.Caller.SendAsync("ReceiveUserPresence", new
        {
            userId = userGuid,
            isOnline = _userPresenceTracker.IsOnline(userGuid),
            updatedAt = DateTime.UtcNow
        });

        var status = await _profileService.GetProviderOperationalStatusAsync(userGuid);
        if (status.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, BuildProviderStatusGroup(userGuid));
            await Clients.Caller.SendAsync("ReceiveProviderStatus", new
            {
                providerId = userGuid,
                status = status.Value.ToString(),
                updatedAt = DateTime.UtcNow
            });
        }

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
        await Groups.AddToGroupAsync(Context.ConnectionId, BuildProviderStatusGroup(providerGuid));

        var status = await _profileService.GetProviderOperationalStatusAsync(providerGuid);
        if (status.HasValue)
        {
            await Clients.Caller.SendAsync("ReceiveProviderStatus", new
            {
                providerId = providerGuid,
                status = status.Value.ToString(),
                updatedAt = DateTime.UtcNow
            });
        }

        return true;
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetHistory(string requestId, string providerId)
    {
        if (!Guid.TryParse(requestId, out var requestGuid)) return Array.Empty<ChatMessageDto>();
        if (!Guid.TryParse(providerId, out var providerGuid)) return Array.Empty<ChatMessageDto>();
        if (!TryGetCurrentUser(out var userGuid, out var role)) return Array.Empty<ChatMessageDto>();

        return await _chatService.GetConversationHistoryAsync(requestGuid, providerGuid, userGuid, role);
    }

    public async Task<string?> GetProviderStatus(string requestId, string providerId)
    {
        if (!Guid.TryParse(requestId, out var requestGuid)) return null;
        if (!Guid.TryParse(providerId, out var providerGuid)) return null;
        if (!TryGetCurrentUser(out var userGuid, out var role)) return null;

        var allowed = await _chatService.CanAccessConversationAsync(requestGuid, providerGuid, userGuid, role);
        if (!allowed) return null;

        var status = await _profileService.GetProviderOperationalStatusAsync(providerGuid);
        return status?.ToString();
    }

    public async Task<object?> GetConversationParticipantPresence(string requestId, string providerId)
    {
        if (!Guid.TryParse(requestId, out var requestGuid)) return null;
        if (!Guid.TryParse(providerId, out var providerGuid)) return null;
        if (!TryGetCurrentUser(out var userGuid, out var role)) return null;

        var allowed = await _chatService.CanAccessConversationAsync(requestGuid, providerGuid, userGuid, role);
        if (!allowed) return null;

        var counterpartId = await _chatService.ResolveRecipientIdAsync(requestGuid, providerGuid, userGuid);
        if (!counterpartId.HasValue) return null;

        var counterpartRole = counterpartId.Value == providerGuid ? "Provider" : "Client";
        string? operationalStatus = null;
        if (counterpartRole == "Provider")
        {
            operationalStatus = (await _profileService.GetProviderOperationalStatusAsync(counterpartId.Value))?.ToString();
        }

        return new
        {
            userId = counterpartId.Value,
            role = counterpartRole,
            isOnline = _userPresenceTracker.IsOnline(counterpartId.Value),
            operationalStatus,
            updatedAt = DateTime.UtcNow
        };
    }

    public async Task<IReadOnlyList<object>> GetMyActiveConversations()
    {
        if (!TryGetCurrentUser(out var userGuid, out var role))
        {
            return Array.Empty<object>();
        }

        var summaries = await _chatService.GetActiveConversationsAsync(userGuid, role);
        if (summaries.Count == 0)
        {
            return Array.Empty<object>();
        }

        var result = new List<object>(summaries.Count);
        foreach (var summary in summaries)
        {
            string? providerStatus = null;
            if (string.Equals(summary.CounterpartRole, "Provider", StringComparison.OrdinalIgnoreCase))
            {
                providerStatus = (await _profileService.GetProviderOperationalStatusAsync(summary.CounterpartUserId))?.ToString();
            }

            result.Add(new
            {
                requestId = summary.RequestId,
                providerId = summary.ProviderId,
                counterpartUserId = summary.CounterpartUserId,
                counterpartRole = summary.CounterpartRole,
                counterpartName = summary.CounterpartName,
                title = summary.Title,
                lastMessagePreview = summary.LastMessagePreview,
                lastMessageAt = summary.LastMessageAt,
                unreadMessages = summary.UnreadMessages,
                counterpartIsOnline = _userPresenceTracker.IsOnline(summary.CounterpartUserId),
                providerStatus
            });
        }

        return result;
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

            if (_mobilePushNotificationService != null)
            {
                var messagePreview = BuildPushMessagePreview(savedMessage.Text, savedMessage.Attachments.Count);
                await _mobilePushNotificationService.SendToUserAsync(
                    recipientId.Value,
                    $"Nova mensagem de {savedMessage.SenderName}",
                    messagePreview,
                    actionUrl: $"/chat?requestId={requestGuid}&providerId={providerGuid}",
                    data: new Dictionary<string, string>
                    {
                        ["type"] = "chat_message",
                        ["requestId"] = requestGuid.ToString(),
                        ["providerId"] = providerGuid.ToString(),
                        ["senderId"] = savedMessage.SenderId.ToString(),
                        ["senderName"] = savedMessage.SenderName
                    });
            }
        }
    }

    public async Task MarkConversationDelivered(string requestId, string providerId)
    {
        if (!Guid.TryParse(requestId, out var requestGuid)) return;
        if (!Guid.TryParse(providerId, out var providerGuid)) return;
        if (!TryGetCurrentUser(out var userGuid, out var role)) return;

        var receipts = await _chatService.MarkConversationDeliveredAsync(requestGuid, providerGuid, userGuid, role);
        if (receipts.Count == 0) return;

        var groupName = BuildConversationGroup(requestGuid, providerGuid);
        foreach (var receipt in receipts)
        {
            await Clients.Group(groupName).SendAsync("ReceiveMessageReceiptUpdated", receipt);
        }
    }

    public async Task MarkConversationRead(string requestId, string providerId)
    {
        if (!Guid.TryParse(requestId, out var requestGuid)) return;
        if (!Guid.TryParse(providerId, out var providerGuid)) return;
        if (!TryGetCurrentUser(out var userGuid, out var role)) return;

        var receipts = await _chatService.MarkConversationReadAsync(requestGuid, providerGuid, userGuid, role);
        if (receipts.Count == 0) return;

        var groupName = BuildConversationGroup(requestGuid, providerGuid);
        foreach (var receipt in receipts)
        {
            await Clients.Group(groupName).SendAsync("ReceiveMessageReceiptUpdated", receipt);
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

    private static string BuildPushMessagePreview(string? text, int attachmentsCount)
    {
        var normalized = (text ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized.Length > 140 ? $"{normalized[..140]}..." : normalized;
        }

        if (attachmentsCount <= 0)
        {
            return "Nova mensagem recebida.";
        }

        return attachmentsCount == 1
            ? "Novo anexo recebido."
            : $"{attachmentsCount} anexos recebidos.";
    }

    public static string BuildProviderStatusGroup(Guid providerId)
    {
        return $"provider-status:{providerId:N}";
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
