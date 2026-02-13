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
        // For development, we just log the notification
        _logger.LogInformation("NOTIFICATION SENT TO {Recipient}.\nSUBJECT: {Subject}\nMESSAGE: {Message}\nACTION_URL: {ActionUrl}", 
            recipient, subject, message, actionUrl);
        
        return Task.CompletedTask;
    }
}
