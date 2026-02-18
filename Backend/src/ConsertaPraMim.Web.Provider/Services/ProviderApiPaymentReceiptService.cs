using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;

namespace ConsertaPraMim.Web.Provider.Services;

public class ProviderApiPaymentReceiptService : IPaymentReceiptService
{
    private readonly ProviderApiCaller _apiCaller;

    public ProviderApiPaymentReceiptService(ProviderApiCaller apiCaller)
    {
        _apiCaller = apiCaller;
    }

    public async Task<IReadOnlyList<PaymentReceiptDto>> GetByServiceRequestAsync(
        Guid actorUserId,
        string actorRole,
        Guid serviceRequestId,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiCaller.SendAsync<List<PaymentReceiptDto>>(
            HttpMethod.Get,
            $"/api/payments/requests/{serviceRequestId}/receipts",
            cancellationToken: cancellationToken);

        return response.Payload ?? [];
    }

    public async Task<PaymentReceiptResultDto> GetByTransactionAsync(
        Guid actorUserId,
        string actorRole,
        Guid serviceRequestId,
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiCaller.SendAsync<PaymentReceiptDto>(
            HttpMethod.Get,
            $"/api/payments/requests/{serviceRequestId}/receipts/{transactionId}",
            cancellationToken: cancellationToken);

        if (!response.Success || response.Payload == null)
        {
            return new PaymentReceiptResultDto(
                false,
                null,
                "api_error",
                response.ErrorMessage ?? "Nao foi possivel carregar o comprovante.");
        }

        return new PaymentReceiptResultDto(true, response.Payload);
    }
}
