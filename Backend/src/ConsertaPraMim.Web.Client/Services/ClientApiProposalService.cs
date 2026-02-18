using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;

namespace ConsertaPraMim.Web.Client.Services;

public class ClientApiProposalService : IProposalService
{
    private readonly ClientApiCaller _apiCaller;

    public ClientApiProposalService(ClientApiCaller apiCaller)
    {
        _apiCaller = apiCaller;
    }

    public async Task<Guid> CreateAsync(Guid providerId, CreateProposalDto dto)
    {
        var response = await _apiCaller.SendAsync<CreateIdResponse>(HttpMethod.Post, "/api/proposals", dto);
        if (!response.Success || response.Payload == null || response.Payload.Id == Guid.Empty)
        {
            throw new InvalidOperationException(response.ErrorMessage ?? "Nao foi possivel enviar a proposta.");
        }

        return response.Payload.Id;
    }

    public async Task<IEnumerable<ProposalDto>> GetByRequestAsync(Guid requestId, Guid actorUserId, string actorRole)
    {
        var response = await _apiCaller.SendAsync<List<ProposalDto>>(HttpMethod.Get, $"/api/proposals/request/{requestId}");
        return response.Payload ?? [];
    }

    public Task<IEnumerable<ProposalDto>> GetByRequestIdAsync(Guid requestId, Guid actorUserId, string actorRole) =>
        GetByRequestAsync(requestId, actorUserId, actorRole);

    public async Task<IEnumerable<ProposalDto>> GetByProviderAsync(Guid providerId)
    {
        var response = await _apiCaller.SendAsync<List<ProposalDto>>(HttpMethod.Get, "/api/proposals/my-proposals");
        return response.Payload ?? [];
    }

    public async Task<bool> AcceptAsync(Guid proposalId, Guid clientId)
    {
        var response = await _apiCaller.SendAsync<object>(HttpMethod.Put, $"/api/proposals/{proposalId}/accept");
        return response.Success;
    }

    private sealed record CreateIdResponse(Guid Id);
}
