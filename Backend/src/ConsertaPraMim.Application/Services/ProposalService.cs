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
    private readonly INotificationService _notificationService;

    public ProposalService(
        IProposalRepository proposalRepository, 
        IServiceRequestRepository requestRepository,
        INotificationService notificationService)
    {
        _proposalRepository = proposalRepository;
        _requestRepository = requestRepository;
        _notificationService = notificationService;
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

        // Notify Client
        var request = await _requestRepository.GetByIdAsync(dto.RequestId);
        if (request != null)
        {
            await _notificationService.SendNotificationAsync(
                request.Client.Email,
                "Nova Proposta Recebida!",
                $"Você recebeu uma nova proposta para o serviço: {request.Description}. Acesse o app para conferir.");
        }

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

    public Task<IEnumerable<ProposalDto>> GetByRequestIdAsync(Guid requestId)
    {
        return GetByRequestAsync(requestId);
    }

    public async Task<IEnumerable<ProposalDto>> GetByProviderAsync(Guid providerId)
    {
        var proposals = await _proposalRepository.GetByProviderIdAsync(providerId);
        return proposals.Select(p => new ProposalDto(
            p.Id,
            p.RequestId,
            p.ProviderId,
            p.Provider?.Name ?? string.Empty,
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

        // Notify Provider
        await _notificationService.SendNotificationAsync(
            proposal.Provider.Email,
            "Sua Proposta foi Aceita!",
            $"Parabéns! O cliente aceitou sua proposta para o serviço: {request.Description}. Entre em contato para combinar os detalhes.");

        return true;
    }
}
