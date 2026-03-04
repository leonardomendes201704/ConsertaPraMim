namespace ConsertaPraMim.Web.TelegramBridge.Hubs;

using ConsertaPraMim.Web.TelegramBridge.Security;
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

        var allowedChatId = ResolveAllowedChatId();
        if (allowedChatId != parsedChatId)
        {
            throw new HubException("chat_id_nao_autorizado");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, BuildConversationGroup(parsedChatId));
    }

    public async Task LeaveConversation(string chatId)
    {
        if (!long.TryParse(chatId, out var parsedChatId))
        {
            return;
        }

        var allowedChatId = ResolveAllowedChatId();
        if (allowedChatId != parsedChatId)
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildConversationGroup(parsedChatId));
    }

    private long ResolveAllowedChatId()
    {
        var user = Context.User;
        if (user is null || !TelegramBridgeClientConversation.TryGetClientId(user, out var clientId))
        {
            throw new HubException("sessao_sem_client_id");
        }

        return TelegramBridgeClientConversation.BuildChatId(clientId);
    }
}
