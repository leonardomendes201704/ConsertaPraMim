using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IPaymentReceiptService
{
    Task<IReadOnlyList<PaymentReceiptDto>> GetByServiceRequestAsync(
        Guid actorUserId,
        string actorRole,
        Guid serviceRequestId,
        CancellationToken cancellationToken = default);

    Task<PaymentReceiptResultDto> GetByTransactionAsync(
        Guid actorUserId,
        string actorRole,
        Guid serviceRequestId,
        Guid transactionId,
        CancellationToken cancellationToken = default);
}
