using ConsertaPraMim.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using ConsertaPraMim.Infrastructure.Hubs;

namespace ConsertaPraMim.Infrastructure.Services;

public class HubNotificationService : INotificationService
{
    private readonly ILogger<HubNotificationService> _logger;
    private readonly IHubContext<NotificationHub> _hubContext;

    public HubNotificationService(ILogger<HubNotificationService> logger, IHubContext<NotificationHub> hubContext)
    {
        _logger = logger;
        _hubContext = hubContext;
    }

    public async Task SendNotificationAsync(string recipient, string subject, string message)
    {
        // 1. Log (Mock Email)
        _logger.LogInformation("REAL-TIME NOTIFICATION TO {Recipient}.\nSUBJECT: {Subject}\nMESSAGE: {Message}", 
            recipient, subject, message);

        // 2. Real-time SignalR (We use recipient as group name - usually should be UserId or Email)
        // In this architecture, it is safer to use the email or a dedicated group.
        await _hubContext.Clients.Group(recipient).SendAsync("ReceiveNotification", new {
            subject,
            message,
            timestamp = DateTime.Now
        });
    }
}
