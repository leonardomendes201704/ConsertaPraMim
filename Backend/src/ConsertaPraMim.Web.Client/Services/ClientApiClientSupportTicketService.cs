using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;

namespace ConsertaPraMim.Web.Client.Services;

public class ClientApiClientSupportTicketService : IClientSupportTicketService
{
    private readonly ClientApiCaller _apiCaller;

    public ClientApiClientSupportTicketService(ClientApiCaller apiCaller)
    {
        _apiCaller = apiCaller;
    }

    public async Task<ClientSupportTicketDetailsDto?> GetByServiceRequestAsync(Guid clientUserId, Guid serviceRequestId)
    {
        _ = clientUserId;

        var response = await _apiCaller.SendAsync<ClientSupportTicketDetailsDto>(
            HttpMethod.Get,
            $"/api/client/support-tickets/service-requests/{serviceRequestId}");

        return response.Payload;
    }

    public async Task<ClientSupportTicketOperationResultDto> AddMessageAsync(
        Guid clientUserId,
        Guid serviceRequestId,
        ClientSupportTicketMessageRequestDto request)
    {
        _ = clientUserId;

        var response = await _apiCaller.SendAsync<ClientSupportTicketOperationResultDto>(
            HttpMethod.Post,
            $"/api/client/support-tickets/service-requests/{serviceRequestId}/messages",
            request);

        if (response.Payload != null)
        {
            return response.Payload;
        }

        return new ClientSupportTicketOperationResultDto(
            false,
            ErrorCode: "client_support_api_error",
            ErrorMessage: response.ErrorMessage ?? "Nao foi possivel atualizar o atendimento de ajuda.");
    }
}
