using ConsertaPraMim.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using ConsertaPraMim.Infrastructure.Hubs;

namespace ConsertaPraMim.Infrastructure.Services;

public class HubNotificationService : INotificationService
{
    private readonly ILogger<HubNotificationService> _logger;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IMobilePushNotificationService? _mobilePushNotificationService;

    public HubNotificationService(
        ILogger<HubNotificationService> logger,
        IHubContext<NotificationHub> hubContext,
        IMobilePushNotificationService? mobilePushNotificationService = null)
    {
        _logger = logger;
        _hubContext = hubContext;
        _mobilePushNotificationService = mobilePushNotificationService;
    }

    public async Task SendNotificationAsync(string recipient, string subject, string message, string? actionUrl = null)
    {
        if (string.IsNullOrWhiteSpace(recipient))
        {
            _logger.LogWarning("Notification recipient is empty. Notification not sent.");
            return;
        }

        var groupName = ResolveGroupName(recipient);
        var normalizedActionUrl = NormalizeActionUrl(actionUrl);
        if (!string.IsNullOrWhiteSpace(actionUrl) && normalizedActionUrl == null)
        {
            _logger.LogWarning("Notification action URL ignored because it is invalid: {ActionUrl}", actionUrl);
        }

        // 1. Log (Mock Email)
        _logger.LogInformation("REAL-TIME NOTIFICATION TO {Recipient}.\nSUBJECT: {Subject}\nMESSAGE: {Message}", 
            groupName, subject, message);

        // 2. Real-time SignalR (We use recipient as group name - usually should be UserId or Email)
        // In this architecture, it is safer to use the email or a dedicated group.
        await _hubContext.Clients.Group(groupName).SendAsync("ReceiveNotification", new {
            subject,
            message,
            actionUrl = normalizedActionUrl,
            timestamp = DateTime.Now
        });

        if (_mobilePushNotificationService != null && Guid.TryParse(recipient?.Trim(), out var recipientUserId))
        {
            await _mobilePushNotificationService.SendToUserAsync(
                recipientUserId,
                subject,
                message,
                normalizedActionUrl,
                new Dictionary<string, string>
                {
                    ["type"] = "system_notification"
                });
        }
    }

    private static string ResolveGroupName(string recipient)
    {
        var normalized = recipient.Trim();
        if (Guid.TryParse(normalized, out var userId))
        {
            return NotificationHub.BuildUserGroup(userId);
        }

        // Legacy fallback (pre-hardening notifications that still use email as group).
        return normalized.ToLowerInvariant();
    }

    private static string? NormalizeActionUrl(string? actionUrl)
    {
        if (string.IsNullOrWhiteSpace(actionUrl))
        {
            return null;
        }

        var trimmed = actionUrl.Trim();
        return trimmed.StartsWith('/') && !trimmed.StartsWith("//")
            ? trimmed
            : null;
    }
}
