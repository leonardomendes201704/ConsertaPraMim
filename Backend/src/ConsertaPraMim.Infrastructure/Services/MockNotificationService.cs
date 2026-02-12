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

    public Task SendNotificationAsync(string recipient, string subject, string message)
    {
        // For development, we just log the notification
        _logger.LogInformation("NOTIFICATION SENT TO {Recipient}.\nSUBJECT: {Subject}\nMESSAGE: {Message}", 
            recipient, subject, message);
        
        return Task.CompletedTask;
    }
}
