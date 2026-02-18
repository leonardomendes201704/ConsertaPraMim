using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ConsertaPraMim.Infrastructure.Hubs;

[Authorize(Roles = "Admin")]
public class AdminMonitoringHub : Hub
{
    public const string AdminGroupName = "admin-monitoring";

    public async Task JoinAdminMonitoringGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroupName);
    }

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole("Admin") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroupName);
        }

        await base.OnConnectedAsync();
    }
}
