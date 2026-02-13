using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IProposalService
{
    Task<Guid> CreateAsync(Guid providerId, CreateProposalDto dto);
    Task<IEnumerable<ProposalDto>> GetByRequestAsync(Guid requestId, Guid actorUserId, string actorRole);
    Task<IEnumerable<ProposalDto>> GetByRequestIdAsync(Guid requestId, Guid actorUserId, string actorRole);
    Task<IEnumerable<ProposalDto>> GetByProviderAsync(Guid providerId);
    Task<bool> AcceptAsync(Guid proposalId, Guid clientId);
}
