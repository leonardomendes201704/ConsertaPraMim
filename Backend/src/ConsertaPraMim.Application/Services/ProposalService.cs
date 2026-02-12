using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.Services;

public class ProposalService : IProposalService
{
    private readonly IProposalRepository _proposalRepository;
    private readonly IServiceRequestRepository _requestRepository;

    public ProposalService(IProposalRepository proposalRepository, IServiceRequestRepository requestRepository)
    {
        _proposalRepository = proposalRepository;
        _requestRepository = requestRepository;
    }

    public async Task<Guid> CreateAsync(Guid providerId, CreateProposalDto dto)
    {
        var proposal = new Proposal
        {
            RequestId = dto.RequestId,
            ProviderId = providerId,
            EstimatedValue = dto.EstimatedValue,
            Message = dto.Message,
            Accepted = false
        };

        await _proposalRepository.AddAsync(proposal);
        return proposal.Id;
    }

    public async Task<IEnumerable<ProposalDto>> GetByRequestAsync(Guid requestId)
    {
        var proposals = await _proposalRepository.GetByRequestIdAsync(requestId);
        return proposals.Select(p => new ProposalDto(
            p.Id,
            p.RequestId,
            p.ProviderId,
            p.Provider.Name,
            p.EstimatedValue,
            p.Accepted,
            p.Message,
            p.CreatedAt));
    }

    public async Task<IEnumerable<ProposalDto>> GetByProviderAsync(Guid providerId)
    {
        var proposals = await _proposalRepository.GetByProviderIdAsync(providerId);
        return proposals.Select(p => new ProposalDto(
            p.Id,
            p.RequestId,
            p.ProviderId,
            p.Provider.Name,
            p.EstimatedValue,
            p.Accepted,
            p.Message,
            p.CreatedAt));
    }

    public async Task<bool> AcceptAsync(Guid proposalId, Guid clientId)
    {
        var proposal = await _proposalRepository.GetByIdAsync(proposalId);
        if (proposal == null) return false;

        // Security check: only the client who created the request can accept a proposal
        if (proposal.Request.ClientId != clientId) return false;

        // Update proposal
        proposal.Accepted = true;
        await _proposalRepository.UpdateAsync(proposal);

        // Update request status
        var request = proposal.Request;
        request.Status = ServiceRequestStatus.Scheduled;
        await _requestRepository.UpdateAsync(request);

        return true;
    }
}
