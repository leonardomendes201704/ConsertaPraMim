using Microsoft.AspNetCore.SignalR;

namespace ConsertaPraMim.Infrastructure.Hubs;

public class ChatHub : Hub
{
    public async Task SendMessage(string requestId, string message)
    {
        var userName = Context.User?.Identity?.Name;
        // Broadcast message to everyone subscribed to this request's chat
        await Clients.Group(requestId).SendAsync("ReceiveChatMessage", userName, message, DateTime.Now);
    }

    public async Task JoinRequestChat(string requestId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, requestId);
    }
}
