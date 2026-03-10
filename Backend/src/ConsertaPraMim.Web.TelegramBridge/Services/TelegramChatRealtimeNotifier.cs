using ConsertaPraMim.Web.TelegramBridge.Hubs;
using ConsertaPraMim.Web.TelegramBridge.Models;
using Microsoft.AspNetCore.SignalR;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramChatRealtimeNotifier : ITelegramChatRealtimeNotifier
{
    private readonly IHubContext<TelegramChatHub> _hubContext;

    public TelegramChatRealtimeNotifier(IHubContext<TelegramChatHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task BroadcastConversationUpsertedAsync(
        ChatConversationSummaryDto summary,
        CancellationToken cancellationToken)
    {
        await _hubContext.Clients.All.SendAsync("ConversationUpserted", summary, cancellationToken);
    }

    public async Task BroadcastConversationMessageAsync(
        ChatConversationSummaryDto summary,
        ChatMessageDto message,
        CancellationToken cancellationToken)
    {
        var group = TelegramChatHub.BuildConversationGroup(message.ChatId);

        await Task.WhenAll(
            _hubContext.Clients.Group(group).SendAsync("ReceiveConversationMessage", message, cancellationToken),
            _hubContext.Clients.All.SendAsync("ConversationUpserted", summary, cancellationToken));
    }
}
