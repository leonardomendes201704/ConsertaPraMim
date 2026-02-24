using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.Services;

public class ProposalService : IProposalService
{
    private readonly IProposalRepository _proposalRepository;
    private readonly IUserRepository _userRepository;
    private readonly IServiceRequestRepository _requestRepository;
    private readonly INotificationService _notificationService;
    private readonly IServiceRequestCommercialValueService _serviceRequestCommercialValueService;
    private readonly IAdminOperationalEventNotifier _adminOperationalEventNotifier;

    public ProposalService(
        IProposalRepository proposalRepository, 
        IUserRepository userRepository,
        IServiceRequestRepository requestRepository,
        INotificationService notificationService,
        IServiceRequestCommercialValueService serviceRequestCommercialValueService,
        IAdminOperationalEventNotifier? adminOperationalEventNotifier = null)
    {
        _proposalRepository = proposalRepository;
        _userRepository = userRepository;
        _requestRepository = requestRepository;
        _notificationService = notificationService;
        _serviceRequestCommercialValueService = serviceRequestCommercialValueService;
        _adminOperationalEventNotifier = adminOperationalEventNotifier ?? NullAdminOperationalEventNotifier.Instance;
    }

    public async Task<Guid> CreateAsync(Guid providerId, CreateProposalDto dto)
    {
        var normalizedLeadTime = NormalizeEstimatedLeadTimeHours(dto.EstimatedLeadTimeHours);
        var normalizedWarranty = NormalizeWarrantyDays(dto.WarrantyDays);
        var provider = await _userRepository.GetByIdAsync(providerId);
        var quality = CalculateProposalQuality(
            dto.Message,
            normalizedLeadTime,
            normalizedWarranty,
            dto.EstimatedValue,
            provider?.ProviderProfile?.Rating ?? 0d,
            provider?.ProviderProfile?.ReviewCount ?? 0);

        var proposal = new Proposal
        {
            RequestId = dto.RequestId,
            ProviderId = providerId,
            EstimatedValue = dto.EstimatedValue,
            Message = dto.Message,
            EstimatedLeadTimeHours = normalizedLeadTime,
            WarrantyDays = normalizedWarranty,
            Accepted = false,
            QualityScore = quality.QualityScore,
            QualityCompletenessScore = quality.CompletenessScore,
            QualityClarityScore = quality.ClarityScore,
            QualityHistoryScore = quality.HistoryScore,
            QualityCommercialScore = quality.CommercialScore,
            QualityCalculatedAtUtc = DateTime.UtcNow
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
        var rankedProposals = proposals
            .Where(p => !p.IsInvalidated)
            .OrderByDescending(GetRankingScore)
            .ThenBy(p => p.EstimatedValue ?? decimal.MaxValue)
            .ThenBy(p => p.CreatedAt)
            .ToList();

        if (actorRole.Equals(UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return rankedProposals.Select(MapToDto);
        }

        if (actorRole.Equals(UserRole.Client.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            if (request.ClientId != actorUserId)
            {
                return Array.Empty<ProposalDto>();
            }

            return rankedProposals.Select(MapToDto);
        }

        if (actorRole.Equals(UserRole.Provider.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return rankedProposals
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
            p.CreatedAt,
            p.EstimatedLeadTimeHours,
            p.WarrantyDays,
            p.IsInvalidated,
            p.QualityScore,
            p.QualityCompletenessScore,
            p.QualityClarityScore,
            p.QualityHistoryScore,
            p.QualityCommercialScore,
            p.QualityCalculatedAtUtc,
            p.Provider?.ProviderProfile?.TrustStatus ?? ProviderTrustStatus.Pending,
            p.Provider?.ProviderProfile?.RiskLevel ?? ProviderRiskLevel.Low,
            p.Provider?.ProviderProfile?.TrustStatusUpdatedAtUtc,
            p.Provider?.ProviderProfile?.TrustStatusReason));
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
            proposal.CreatedAt,
            proposal.EstimatedLeadTimeHours,
            proposal.WarrantyDays,
            proposal.IsInvalidated,
            proposal.QualityScore,
            proposal.QualityCompletenessScore,
            proposal.QualityClarityScore,
            proposal.QualityHistoryScore,
            proposal.QualityCommercialScore,
            proposal.QualityCalculatedAtUtc,
            proposal.Provider?.ProviderProfile?.TrustStatus ?? ProviderTrustStatus.Pending,
            proposal.Provider?.ProviderProfile?.RiskLevel ?? ProviderRiskLevel.Low,
            proposal.Provider?.ProviderProfile?.TrustStatusUpdatedAtUtc,
            proposal.Provider?.ProviderProfile?.TrustStatusReason);
    }

    private static int? NormalizeEstimatedLeadTimeHours(int? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return Math.Clamp(value.Value, 1, 720);
    }

    private static int? NormalizeWarrantyDays(int? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return Math.Clamp(value.Value, 0, 3650);
    }

    private static decimal GetRankingScore(Proposal proposal)
    {
        var qualityScore = proposal.QualityScore ?? 0m;
        var providerRating = proposal.Provider?.ProviderProfile?.Rating ?? 0d;
        var providerReviewCount = proposal.Provider?.ProviderProfile?.ReviewCount ?? 0;
        var ratingScore = providerRating > 0d
            ? Math.Clamp(providerRating / 5d * 100d, 0d, 100d)
            : 35d;
        var historyVolumeScore = providerReviewCount switch
        {
            >= 100 => 100d,
            >= 50 => 90d,
            >= 20 => 75d,
            >= 10 => 65d,
            >= 5 => 55d,
            > 0 => 45d,
            _ => 30d
        };
        var historicalScore = (ratingScore * 0.70d) + (historyVolumeScore * 0.30d);
        var ranking = (double)qualityScore * 0.75d + historicalScore * 0.25d + (proposal.Accepted ? 2d : 0d);
        return decimal.Round((decimal)Math.Clamp(ranking, 0d, 100d), 2, MidpointRounding.AwayFromZero);
    }

    private static ProposalQualityScoreSnapshot CalculateProposalQuality(
        string? message,
        int? estimatedLeadTimeHours,
        int? warrantyDays,
        decimal? estimatedValue,
        double providerRating,
        int providerReviewCount)
    {
        var normalizedMessage = message?.Trim();
        var hasScope = !string.IsNullOrWhiteSpace(normalizedMessage);
        var hasLeadTime = estimatedLeadTimeHours.HasValue;
        var hasWarranty = warrantyDays.HasValue;

        var completeness = ((hasScope ? 1d : 0d) + (hasLeadTime ? 1d : 0d) + (hasWarranty ? 1d : 0d)) / 3d * 100d;

        var clarity = 20d;
        if (hasScope)
        {
            var length = normalizedMessage!.Length;
            clarity = length switch
            {
                >= 120 => 100d,
                >= 80 => 90d,
                >= 40 => 75d,
                >= 20 => 55d,
                _ => 30d
            };
        }

        var ratingScore = providerRating > 0d
            ? Math.Clamp(providerRating / 5d * 100d, 0d, 100d)
            : 35d;
        var historyVolumeScore = providerReviewCount switch
        {
            >= 100 => 100d,
            >= 50 => 90d,
            >= 20 => 75d,
            >= 10 => 65d,
            >= 5 => 55d,
            > 0 => 45d,
            _ => 30d
        };
        var history = (ratingScore * 0.65d) + (historyVolumeScore * 0.35d);

        var commercial = estimatedValue.HasValue && estimatedValue.Value > 0m ? 80d : 30d;
        if (warrantyDays.HasValue && warrantyDays.Value >= 90)
        {
            commercial += 10d;
        }
        else if (warrantyDays.HasValue && warrantyDays.Value >= 30)
        {
            commercial += 5d;
        }
        commercial = Math.Clamp(commercial, 0d, 100d);

        var qualityScore =
            (completeness * 0.40d) +
            (clarity * 0.25d) +
            (history * 0.25d) +
            (commercial * 0.10d);

        return new ProposalQualityScoreSnapshot(
            decimal.Round((decimal)qualityScore, 2, MidpointRounding.AwayFromZero),
            decimal.Round((decimal)completeness, 2, MidpointRounding.AwayFromZero),
            decimal.Round((decimal)clarity, 2, MidpointRounding.AwayFromZero),
            decimal.Round((decimal)history, 2, MidpointRounding.AwayFromZero),
            decimal.Round((decimal)commercial, 2, MidpointRounding.AwayFromZero));
    }

    private readonly record struct ProposalQualityScoreSnapshot(
        decimal QualityScore,
        decimal CompletenessScore,
        decimal ClarityScore,
        decimal HistoryScore,
        decimal CommercialScore);

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

