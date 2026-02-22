using ConsertaPraMim.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConsertaPraMim.Infrastructure.Services;

public class MockNotificationService : INotificationService
{
    private readonly ILogger<MockNotificationService> _logger;

    public MockNotificationService(ILogger<MockNotificationService> logger)
    {
        _logger = logger;
    }

    public Task SendNotificationAsync(string recipient, string subject, string message, string? actionUrl = null)
    {
        return SendNotificationAsync(recipient, subject, message, actionUrl, data: null);
    }

    public Task SendNotificationAsync(
        string recipient,
        string subject,
        string message,
        string? actionUrl,
        IReadOnlyDictionary<string, string>? data)
    {
        // For development, we just log the notification
        _logger.LogInformation(
            "NOTIFICATION SENT TO {Recipient}.\nSUBJECT: {Subject}\nMESSAGE: {Message}\nACTION_URL: {ActionUrl}\nDATA_KEYS: {DataKeys}",
            recipient,
            subject,
            message,
            actionUrl,
            data == null ? "-" : string.Join(",", data.Keys));
        
        return Task.CompletedTask;
    }
}
