using Microsoft.AspNetCore.SignalR;

namespace ConsertaPraMim.Infrastructure.Hubs;

public class NotificationHub : Hub
{
    public async Task JoinUserGroup(string userId)
    {
        var groupName = userId?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(groupName)) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }
}
