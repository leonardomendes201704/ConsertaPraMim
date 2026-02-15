using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IPaymentWebhookService
{
    Task<PaymentWebhookProcessResultDto> ProcessWebhookAsync(
        PaymentWebhookRequestDto request,
        CancellationToken cancellationToken = default);
}
