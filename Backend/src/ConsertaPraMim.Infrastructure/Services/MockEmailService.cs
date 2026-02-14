using ConsertaPraMim.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConsertaPraMim.Infrastructure.Services;

public class MockEmailService : IEmailService
{
    private readonly ILogger<MockEmailService> _logger;

    public MockEmailService(ILogger<MockEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string to, string subject, string body)
    {
        _logger.LogInformation(
            "EMAIL OUTBOX [MOCK] To={To} Subject={Subject} Body={Body}",
            to,
            subject,
            body);

        return Task.CompletedTask;
    }
}
