using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IPaymentService
{
    Task<PaymentCheckoutSessionDto> CreateCheckoutSessionAsync(
        PaymentCheckoutRequestDto request,
        CancellationToken cancellationToken = default);

    bool ValidateWebhookSignature(PaymentWebhookRequestDto request);

    Task<PaymentWebhookEventDto?> ParseWebhookAsync(
        PaymentWebhookRequestDto request,
        CancellationToken cancellationToken = default);

    Task<bool> RefundAsync(
        PaymentRefundRequestDto request,
        CancellationToken cancellationToken = default);

    Task<bool> ReleaseFundsAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default);
}
