using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ConsertaPraMim.Infrastructure.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    public async Task JoinUserGroup()
    {
        var userIdRaw = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdRaw, out var userId)) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, BuildUserGroup(userId));
    }

    public static string BuildUserGroup(Guid userId)
    {
        return $"notify-user:{userId:N}";
    }
}
