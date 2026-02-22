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
    private readonly IServiceRequestCommercialValueService _serviceRequestCommercialValueService;
    private readonly IAdminOperationalEventNotifier _adminOperationalEventNotifier;

    public ProposalService(
        IProposalRepository proposalRepository, 
        IServiceRequestRepository requestRepository,
        INotificationService notificationService,
        IServiceRequestCommercialValueService serviceRequestCommercialValueService,
        IAdminOperationalEventNotifier? adminOperationalEventNotifier = null)
    {
        _proposalRepository = proposalRepository;
        _requestRepository = requestRepository;
        _notificationService = notificationService;
        _serviceRequestCommercialValueService = serviceRequestCommercialValueService;
        _adminOperationalEventNotifier = adminOperationalEventNotifier ?? NullAdminOperationalEventNotifier.Instance;
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
            if (request.Status == ServiceRequestStatus.Created)
            {
                request.Status = ServiceRequestStatus.Matching;
                await _requestRepository.UpdateAsync(request);
            }

            await _notificationService.SendNotificationAsync(
                request.ClientId.ToString("N"),
                "Nova Proposta Recebida!",
                $"Voce recebeu uma nova proposta para o servico: {request.Description}. Acesse o app para conferir.",
                $"/ServiceRequests/Details/{request.Id}");

            await _adminOperationalEventNotifier.NotifyProviderSentProposalAsync(
                proposal.Id,
                request.Id,
                proposal.EstimatedValue);
        }

        return proposal.Id;
    }

    public async Task<IEnumerable<ProposalDto>> GetByRequestAsync(Guid requestId, Guid actorUserId, string actorRole)
    {
        var request = await _requestRepository.GetByIdAsync(requestId);
        if (request == null)
        {
            return Array.Empty<ProposalDto>();
        }

        var proposals = await _proposalRepository.GetByRequestIdAsync(requestId);
        var visibleProposals = proposals
            .Where(p => !p.IsInvalidated)
            .ToList();

        if (actorRole.Equals(UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return visibleProposals.Select(MapToDto);
        }

        if (actorRole.Equals(UserRole.Client.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            if (request.ClientId != actorUserId)
            {
                return Array.Empty<ProposalDto>();
            }

            return visibleProposals.Select(MapToDto);
        }

        if (actorRole.Equals(UserRole.Provider.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return visibleProposals
                .Where(p => p.ProviderId == actorUserId)
                .Select(MapToDto);
        }

        return Array.Empty<ProposalDto>();
    }

    public Task<IEnumerable<ProposalDto>> GetByRequestIdAsync(Guid requestId, Guid actorUserId, string actorRole)
    {
        return GetByRequestAsync(requestId, actorUserId, actorRole);
    }

    public async Task<IEnumerable<ProposalDto>> GetByProviderAsync(Guid providerId)
    {
        var proposals = await _proposalRepository.GetByProviderIdAsync(providerId);
        return proposals
            .Where(p => !p.IsInvalidated)
            .Select(p => new ProposalDto(
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
        if (proposal.IsInvalidated) return false;

        // Security check: only the client who created the request can accept a proposal
        if (proposal.Request.ClientId != clientId) return false;

        // Update proposal
        proposal.Accepted = true;
        proposal.UpdatedAt = DateTime.UtcNow;
        await _proposalRepository.UpdateAsync(proposal);

        // Update request status
        var request = proposal.Request;
        var commercialTotals = await _serviceRequestCommercialValueService.RecalculateAsync(request);
        request.CommercialVersion = Math.Max(1, request.CommercialVersion);
        request.CommercialState = ServiceRequestCommercialState.Stable;
        request.CommercialBaseValue = commercialTotals.BaseValue;
        request.CommercialCurrentValue = commercialTotals.CurrentValue;
        request.CommercialUpdatedAtUtc = DateTime.UtcNow;
        request.Status = ServiceRequestStatus.Scheduled;
        await _requestRepository.UpdateAsync(request);

        // Notify Provider
        await _notificationService.SendNotificationAsync(
            proposal.ProviderId.ToString("N"),
            "Sua Proposta foi Aceita!",
            $"Parabens! O cliente aceitou sua proposta para o servico: {request.Description}. Entre em contato para combinar os detalhes.",
            $"/ServiceRequests/Details/{request.Id}");

        await _adminOperationalEventNotifier.NotifyClientAcceptedProposalAsync(
            proposal.Id,
            request.Id);

        return true;
    }

    private static ProposalDto MapToDto(Proposal proposal)
    {
        return new ProposalDto(
            proposal.Id,
            proposal.RequestId,
            proposal.ProviderId,
            proposal.Provider?.Name ?? string.Empty,
            proposal.EstimatedValue,
            proposal.Accepted,
            proposal.Message,
            proposal.CreatedAt);
    }

    private sealed class NullAdminOperationalEventNotifier : IAdminOperationalEventNotifier
    {
        public static readonly NullAdminOperationalEventNotifier Instance = new();

        public Task NotifyClientOpenedRequestAsync(Guid requestId, string? requestDescription, string? categoryName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifyProviderOpenedSupportTicketAsync(Guid ticketId, Guid providerUserId, string? ticketSubject, string? categoryName, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifyProviderSentProposalAsync(Guid proposalId, Guid requestId, decimal? estimatedValue, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifyClientAcceptedProposalAsync(Guid proposalId, Guid requestId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifyClientScheduledAsync(Guid appointmentId, Guid requestId, DateTime windowStartUtc, DateTime windowEndUtc, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifyUserRegisteredAsync(Guid userId, string userName, string role, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifyUserLoggedInAsync(Guid userId, string userName, string role, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}

