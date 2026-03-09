using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ConsertaPraMim.Infrastructure.Hubs;

[Authorize(Roles = "Admin")]
public sealed class FireTvDashboardHub : Hub
{
    public const string FireTvDashboardGroupName = "fire-tv-dashboard";

    public async Task JoinDashboardGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, FireTvDashboardGroupName);
    }

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole("Admin") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, FireTvDashboardGroupName);
        }

        await base.OnConnectedAsync();
    }
}
