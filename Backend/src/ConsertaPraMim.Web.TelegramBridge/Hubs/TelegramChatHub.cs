namespace ConsertaPraMim.Web.TelegramBridge.Hubs;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

[Authorize]
public sealed class TelegramChatHub : Hub
{
    public static string BuildConversationGroup(long chatId) => $"telegram-conversation-{chatId}";

    public async Task JoinConversation(string chatId)
    {
        if (!long.TryParse(chatId, out var parsedChatId))
        {
            throw new HubException("chat_id_invalido");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, BuildConversationGroup(parsedChatId));
    }

    public async Task LeaveConversation(string chatId)
    {
        if (!long.TryParse(chatId, out var parsedChatId))
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildConversationGroup(parsedChatId));
    }
}
