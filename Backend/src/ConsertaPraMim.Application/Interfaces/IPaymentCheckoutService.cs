using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IPaymentCheckoutService
{
    Task<PaymentCheckoutResultDto> CreateCheckoutAsync(
        Guid actorUserId,
        string actorRole,
        CreatePaymentCheckoutRequestDto request,
        CancellationToken cancellationToken = default);
}
