using ConsertaPraMim.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConsertaPraMim.Infrastructure.Services;

public class MockPaymentService : IPaymentService
{
    private readonly ILogger<MockPaymentService> _logger;

    public MockPaymentService(ILogger<MockPaymentService> logger)
    {
        _logger = logger;
    }

    public Task<string> CreateCheckoutSessionAsync(Guid proposalId, decimal amount, string currency = "brl")
    {
        _logger.LogInformation("MOCK PAYMENT: Creating checkout for proposal {ProposalId} - Amount: {Amount} {Currency}", 
            proposalId, amount, currency);
        
        // Return a mock checkout URL
        return Task.FromResult($"https://checkout.consertapramim.com/pay/{proposalId}");
    }

    public Task<bool> RefundAsync(string paymentIntentId)
    {
        _logger.LogInformation("MOCK PAYMENT: Refunding payment {Id}", paymentIntentId);
        return Task.FromResult(true);
    }

    public Task<bool> ReleaseFundsAsync(Guid proposalId)
    {
        _logger.LogInformation("MOCK PAYMENT: Releasing funds for proposal {Id} to provider.", proposalId);
        return Task.FromResult(true);
    }
}
