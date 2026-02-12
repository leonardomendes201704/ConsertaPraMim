using Microsoft.AspNetCore.SignalR;

namespace ConsertaPraMim.Infrastructure.Hubs;

public class NotificationHub : Hub
{
    public async Task JoinUserGroup(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, userId);
    }
}
