using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class AdminGrowthService : IAdminGrowthService
{
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IProposalRepository _proposalRepository;

    public AdminGrowthService(
        IServiceRequestRepository serviceRequestRepository,
        IProposalRepository proposalRepository)
    {
        _serviceRequestRepository = serviceRequestRepository;
        _proposalRepository = proposalRepository;
    }

    public async Task<AdminGrowthFunnelDto> GetFunnelAsync(AdminGrowthFunnelQueryDto query)
    {
        var (fromUtc, toUtc) = NormalizeRange(query.FromUtc, query.ToUtc);
        var nowUtc = DateTime.UtcNow;
        var proposalSlaMinutes = Math.Clamp(query.ProposalSlaMinutes, 5, 720);
        var acceptanceSlaMinutes = Math.Clamp(query.AcceptanceSlaHours, 1, 168) * 60;

        var requests = (await _serviceRequestRepository.GetAllAsync())
            .Where(request => request.CreatedAt >= fromUtc && request.CreatedAt <= toUtc)
            .Where(request => MatchesCategory(request, query.Category))
            .Where(request => MatchesCity(request, query.City))
            .ToList();

        var requestIds = requests
            .Select(request => request.Id)
            .ToHashSet();

        var proposals = (await _proposalRepository.GetAllAsync())
            .Where(proposal => !proposal.IsInvalidated)
            .Where(proposal => requestIds.Contains(proposal.RequestId))
            .ToList();

        var proposalsByRequest = proposals
            .GroupBy(proposal => proposal.RequestId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.CreatedAt).ToList());

        var firstProposalDurations = new List<decimal>();
        var proposalAcceptanceDurations = new List<decimal>();

        var firstProposalCompleted = 0;
        var firstProposalPending = 0;
        var firstProposalWithinSla = 0;
        var firstProposalBreachedSla = 0;

        var proposalAcceptanceApplicable = 0;
        var proposalAcceptanceCompleted = 0;
        var proposalAcceptancePending = 0;
        var proposalAcceptanceWithinSla = 0;
        var proposalAcceptanceBreachedSla = 0;

        var requestsWithAnyProposal = 0;
        var acceptedRequests = 0;

        foreach (var request in requests)
        {
            if (!proposalsByRequest.TryGetValue(request.Id, out var requestProposals) || requestProposals.Count == 0)
            {
                var elapsedWithoutProposalMinutes = (decimal)(nowUtc - request.CreatedAt).TotalMinutes;
                if (elapsedWithoutProposalMinutes > proposalSlaMinutes)
                {
                    firstProposalBreachedSla++;
                }
                else
                {
                    firstProposalPending++;
                }

                continue;
            }

            requestsWithAnyProposal++;
            proposalAcceptanceApplicable++;

            var firstProposal = requestProposals[0];
            var firstProposalMinutes = (decimal)(firstProposal.CreatedAt - request.CreatedAt).TotalMinutes;

            firstProposalCompleted++;
            firstProposalDurations.Add(firstProposalMinutes);

            if (firstProposalMinutes <= proposalSlaMinutes)
            {
                firstProposalWithinSla++;
            }
            else
            {
                firstProposalBreachedSla++;
            }

            var acceptedProposal = requestProposals
                .Where(proposal => proposal.Accepted)
                .Select(proposal => new
                {
                    Proposal = proposal,
                    AcceptedAt = ResolveProposalAcceptedTimestamp(proposal)
                })
                .OrderBy(item => item.AcceptedAt)
                .FirstOrDefault();

            if (acceptedProposal is null)
            {
                var elapsedWithoutAcceptanceMinutes = (decimal)(nowUtc - firstProposal.CreatedAt).TotalMinutes;
                if (elapsedWithoutAcceptanceMinutes > acceptanceSlaMinutes)
                {
                    proposalAcceptanceBreachedSla++;
                }
                else
                {
                    proposalAcceptancePending++;
                }

                continue;
            }

            acceptedRequests++;
            proposalAcceptanceCompleted++;

            var acceptanceMinutes = (decimal)(acceptedProposal.AcceptedAt - firstProposal.CreatedAt).TotalMinutes;
            proposalAcceptanceDurations.Add(acceptanceMinutes);

            if (acceptanceMinutes <= acceptanceSlaMinutes)
            {
                proposalAcceptanceWithinSla++;
            }
            else
            {
                proposalAcceptanceBreachedSla++;
            }
        }

        var firstProposalStage = BuildStage(
            stage: "Pedido -> primeira proposta",
            applicable: requests.Count,
            completed: firstProposalCompleted,
            pending: firstProposalPending,
            withinSla: firstProposalWithinSla,
            breachedSla: firstProposalBreachedSla,
            durationsMinutes: firstProposalDurations);

        var proposalAcceptanceStage = BuildStage(
            stage: "Primeira proposta -> aceite",
            applicable: proposalAcceptanceApplicable,
            completed: proposalAcceptanceCompleted,
            pending: proposalAcceptancePending,
            withinSla: proposalAcceptanceWithinSla,
            breachedSla: proposalAcceptanceBreachedSla,
            durationsMinutes: proposalAcceptanceDurations);

        var requestsWithoutProposal = requests.Count - requestsWithAnyProposal;
        var scheduledOrBeyondRequests = requests.Count(request => request.Status is ServiceRequestStatus.Scheduled
            or ServiceRequestStatus.InProgress
            or ServiceRequestStatus.Completed
            or ServiceRequestStatus.Validated
            or ServiceRequestStatus.PendingClientCompletionAcceptance);

        var completedRequests = requests.Count(request => request.Status is ServiceRequestStatus.Completed or ServiceRequestStatus.Validated);

        var alerts = BuildAlerts(
            requestsTotal: requests.Count,
            requestsWithoutProposal: requestsWithoutProposal,
            firstProposalStage: firstProposalStage,
            proposalAcceptanceStage: proposalAcceptanceStage);

        return new AdminGrowthFunnelDto(
            FromUtc: fromUtc,
            ToUtc: toUtc,
            CategoryFilter: string.IsNullOrWhiteSpace(query.Category) ? null : query.Category.Trim(),
            CityFilter: string.IsNullOrWhiteSpace(query.City) ? null : query.City.Trim(),
            ProposalSlaMinutes: proposalSlaMinutes,
            AcceptanceSlaMinutes: acceptanceSlaMinutes,
            RequestsTotal: requests.Count,
            RequestsWithAnyProposal: requestsWithAnyProposal,
            RequestsWithoutProposal: requestsWithoutProposal,
            AcceptedRequests: acceptedRequests,
            ScheduledOrBeyondRequests: scheduledOrBeyondRequests,
            CompletedRequests: completedRequests,
            FirstProposalStage: firstProposalStage,
            ProposalAcceptanceStage: proposalAcceptanceStage,
            Alerts: alerts);
    }

    private static AdminGrowthFunnelStageDto BuildStage(
        string stage,
        int applicable,
        int completed,
        int pending,
        int withinSla,
        int breachedSla,
        IReadOnlyList<decimal> durationsMinutes)
    {
        var withinRate = applicable == 0
            ? 0m
            : Math.Round((decimal)withinSla * 100m / applicable, 2, MidpointRounding.AwayFromZero);

        var averageMinutes = durationsMinutes.Count == 0
            ? (decimal?)null
            : Math.Round(durationsMinutes.Average(), 2, MidpointRounding.AwayFromZero);

        var p50Minutes = durationsMinutes.Count == 0
            ? (decimal?)null
            : ResolveMedian(durationsMinutes);

        return new AdminGrowthFunnelStageDto(
            Stage: stage,
            Applicable: applicable,
            Completed: completed,
            Pending: pending,
            WithinSla: withinSla,
            BreachedSla: breachedSla,
            WithinSlaRatePercent: withinRate,
            AverageDurationMinutes: averageMinutes,
            P50DurationMinutes: p50Minutes);
    }

    private static IReadOnlyList<AdminGrowthAlertDto> BuildAlerts(
        int requestsTotal,
        int requestsWithoutProposal,
        AdminGrowthFunnelStageDto firstProposalStage,
        AdminGrowthFunnelStageDto proposalAcceptanceStage)
    {
        var alerts = new List<AdminGrowthAlertDto>();

        if (requestsTotal > 0)
        {
            var noProposalRate = Math.Round((decimal)requestsWithoutProposal * 100m / requestsTotal, 2, MidpointRounding.AwayFromZero);
            if (noProposalRate >= 45m)
            {
                alerts.Add(new AdminGrowthAlertDto(
                    Code: "funnel_no_proposal_rate_critical",
                    Severity: "critical",
                    Title: "Taxa de pedidos sem proposta esta critica",
                    Description: "Ha excesso de pedidos sem qualquer proposta no periodo filtrado. Revisar liquidez por categoria e regiao.",
                    CurrentValue: noProposalRate,
                    ThresholdValue: 45m,
                    Unit: "%"));
            }
            else if (noProposalRate >= 30m)
            {
                alerts.Add(new AdminGrowthAlertDto(
                    Code: "funnel_no_proposal_rate_warning",
                    Severity: "warning",
                    Title: "Taxa de pedidos sem proposta acima do esperado",
                    Description: "Parte relevante dos pedidos nao recebeu proposta. Recomenda-se acao comercial/regional.",
                    CurrentValue: noProposalRate,
                    ThresholdValue: 30m,
                    Unit: "%"));
            }
        }

        if (firstProposalStage.WithinSlaRatePercent < 60m)
        {
            alerts.Add(new AdminGrowthAlertDto(
                Code: "funnel_first_proposal_sla_critical",
                Severity: "critical",
                Title: "SLA da primeira proposta abaixo do minimo",
                Description: "A etapa pedido -> primeira proposta esta com desempenho critico e impacta a conversao inicial.",
                CurrentValue: firstProposalStage.WithinSlaRatePercent,
                ThresholdValue: 60m,
                Unit: "%"));
        }
        else if (firstProposalStage.WithinSlaRatePercent < 75m)
        {
            alerts.Add(new AdminGrowthAlertDto(
                Code: "funnel_first_proposal_sla_warning",
                Severity: "warning",
                Title: "SLA da primeira proposta em nivel de atencao",
                Description: "A etapa pedido -> primeira proposta esta abaixo da meta recomendada para liquidez saudavel.",
                CurrentValue: firstProposalStage.WithinSlaRatePercent,
                ThresholdValue: 75m,
                Unit: "%"));
        }

        if (proposalAcceptanceStage.WithinSlaRatePercent < 50m)
        {
            alerts.Add(new AdminGrowthAlertDto(
                Code: "funnel_acceptance_sla_critical",
                Severity: "critical",
                Title: "SLA de aceite de propostas esta critico",
                Description: "Clientes estao demorando para aceitar propostas apos o primeiro envio, com risco de perda de conversao.",
                CurrentValue: proposalAcceptanceStage.WithinSlaRatePercent,
                ThresholdValue: 50m,
                Unit: "%"));
        }
        else if (proposalAcceptanceStage.WithinSlaRatePercent < 70m)
        {
            alerts.Add(new AdminGrowthAlertDto(
                Code: "funnel_acceptance_sla_warning",
                Severity: "warning",
                Title: "SLA de aceite de propostas em atencao",
                Description: "A etapa primeira proposta -> aceite esta abaixo da meta recomendada e precisa de ajuste de experiencia/oferta.",
                CurrentValue: proposalAcceptanceStage.WithinSlaRatePercent,
                ThresholdValue: 70m,
                Unit: "%"));
        }

        return alerts;
    }

    private static DateTime ResolveProposalAcceptedTimestamp(Proposal proposal)
    {
        if (proposal.UpdatedAt.HasValue && proposal.UpdatedAt.Value >= proposal.CreatedAt)
        {
            return proposal.UpdatedAt.Value;
        }

        return proposal.CreatedAt;
    }

    private static bool MatchesCategory(ServiceRequest request, string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return true;
        }

        var normalizedFilter = category.Trim();
        if (ServiceCategoryExtensions.TryParseFlexible(normalizedFilter, out var parsedCategory)
            && request.Category == parsedCategory)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(request.CategoryDefinition?.Name)
            && request.CategoryDefinition.Name.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (request.Category.ToString().Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return request.Category.ToPtBr().Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesCity(ServiceRequest request, string? city)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            return true;
        }

        return (request.AddressCity ?? string.Empty)
            .Contains(city.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static (DateTime fromUtc, DateTime toUtc) NormalizeRange(DateTime? fromUtc, DateTime? toUtc)
    {
        var nowUtc = DateTime.UtcNow;
        var normalizedTo = toUtc?.ToUniversalTime() ?? nowUtc;
        var normalizedFrom = fromUtc?.ToUniversalTime() ?? normalizedTo.AddDays(-7);

        if (normalizedFrom > normalizedTo)
        {
            (normalizedFrom, normalizedTo) = (normalizedTo, normalizedFrom);
        }

        return (normalizedFrom, normalizedTo);
    }

    private static decimal ResolveMedian(IReadOnlyList<decimal> values)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        if (ordered.Length == 0)
        {
            return 0m;
        }

        var middle = ordered.Length / 2;
        if (ordered.Length % 2 == 0)
        {
            return Math.Round((ordered[middle - 1] + ordered[middle]) / 2m, 2, MidpointRounding.AwayFromZero);
        }

        return Math.Round(ordered[middle], 2, MidpointRounding.AwayFromZero);
    }
}
