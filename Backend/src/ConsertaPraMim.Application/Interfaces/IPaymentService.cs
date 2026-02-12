namespace ConsertaPraMim.Application.Interfaces;

public interface IPaymentService
{
    Task<string> CreateCheckoutSessionAsync(Guid proposalId, decimal amount, string currency = "brl");
    Task<bool> RefundAsync(string paymentIntentId);
    Task<bool> ReleaseFundsAsync(Guid proposalId);
}
