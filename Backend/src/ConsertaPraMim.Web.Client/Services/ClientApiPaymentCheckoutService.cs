using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;

namespace ConsertaPraMim.Web.Client.Services;

public class ClientApiPaymentCheckoutService : IPaymentCheckoutService
{
    private readonly ClientApiCaller _apiCaller;

    public ClientApiPaymentCheckoutService(ClientApiCaller apiCaller)
    {
        _apiCaller = apiCaller;
    }

    public async Task<PaymentCheckoutResultDto> CreateCheckoutAsync(
        Guid actorUserId,
        string actorRole,
        CreatePaymentCheckoutRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiCaller.SendAsync<PaymentCheckoutResultDto>(
            HttpMethod.Post,
            "/api/payments/checkout",
            request,
            cancellationToken);

        return response.Payload ?? new PaymentCheckoutResultDto(false, ErrorCode: "api_error", ErrorMessage: response.ErrorMessage);
    }
}
