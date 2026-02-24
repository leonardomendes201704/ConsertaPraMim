using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class AdminLiquidityScoreService : IAdminLiquidityScoreService
{
    private const decimal CoverageWeight = 0.55m;
    private const decimal SupplyWeight = 0.20m;
    private const decimal ResponseWeight = 0.25m;

    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IProposalRepository _proposalRepository;

    public AdminLiquidityScoreService(
        IServiceRequestRepository serviceRequestRepository,
        IProposalRepository proposalRepository)
    {
        _serviceRequestRepository = serviceRequestRepository;
        _proposalRepository = proposalRepository;
    }

    public async Task<AdminLiquidityScoreResponseDto> GetScoreAsync(AdminLiquidityScoreQueryDto query)
    {
        var (fromUtc, toUtc) = NormalizeRange(query.FromUtc, query.ToUtc);
        var proposalSlaMinutes = Math.Clamp(query.ProposalSlaMinutes, 5, 720);
        var take = Math.Clamp(query.Take, 1, 200);

        var requests = (await _serviceRequestRepository.GetAllAsync())
            .Where(request => request.CreatedAt >= fromUtc && request.CreatedAt <= toUtc)
            .Where(request => MatchesCategory(request, query.Category))
            .Where(request => MatchesCity(request, query.City))
            .ToList();

        var requestIds = requests.Select(request => request.Id).ToHashSet();
        var proposals = (await _proposalRepository.GetAllAsync())
            .Where(proposal => !proposal.IsInvalidated)
            .Where(proposal => requestIds.Contains(proposal.RequestId))
            .ToList();

        var proposalsByRequest = proposals
            .GroupBy(proposal => proposal.RequestId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(item => item.CreatedAt).ToList());

        var groupedItems = requests
            .GroupBy(request => new
            {
                Region = NormalizeRegion(request.AddressCity),
                Category = ResolveCategoryLabel(request)
            })
            .Select(group => BuildItem(
                region: group.Key.Region,
                category: group.Key.Category,
                requests: group.ToList(),
                proposalsByRequest: proposalsByRequest,
                proposalSlaMinutes: proposalSlaMinutes))
            .OrderBy(item => item.LiquidityScore)
            .ThenByDescending(item => item.DemandRequests)
            .Take(take)
            .ToList();

        var history = BuildHistory(
            requests: requests,
            proposalsByRequest: proposalsByRequest,
            proposalSlaMinutes: proposalSlaMinutes);

        var alerts = BuildAlerts(groupedItems, requests.Count);

        return new AdminLiquidityScoreResponseDto(
            FromUtc: fromUtc,
            ToUtc: toUtc,
            CategoryFilter: string.IsNullOrWhiteSpace(query.Category) ? null : query.Category.Trim(),
            CityFilter: string.IsNullOrWhiteSpace(query.City) ? null : query.City.Trim(),
            ProposalSlaMinutes: proposalSlaMinutes,
            FormulaDescription: "Score = cobertura_propostas(55%) + profundidade_oferta(20%) + velocidade_primeira_proposta(25%).",
            Items: groupedItems,
            History: history,
            Alerts: alerts);
    }

    private static AdminLiquidityScoreItemDto BuildItem(
        string region,
        string category,
        IReadOnlyList<ServiceRequest> requests,
        IReadOnlyDictionary<Guid, List<Proposal>> proposalsByRequest,
        int proposalSlaMinutes)
    {
        var demandRequests = requests.Count;
        var withProposal = 0;
        var durationsMinutes = new List<decimal>();
        var distinctProviders = new HashSet<Guid>();
        var withinSlaCount = 0;

        foreach (var request in requests)
        {
            if (!proposalsByRequest.TryGetValue(request.Id, out var proposals) || proposals.Count == 0)
            {
                continue;
            }

            withProposal++;
            foreach (var providerId in proposals.Select(proposal => proposal.ProviderId))
            {
                distinctProviders.Add(providerId);
            }

            var firstProposal = proposals[0];
            var elapsedMinutes = (decimal)(firstProposal.CreatedAt - request.CreatedAt).TotalMinutes;
            durationsMinutes.Add(elapsedMinutes);
            if (elapsedMinutes <= proposalSlaMinutes)
            {
                withinSlaCount++;
            }
        }

        var withoutProposal = demandRequests - withProposal;
        var proposalCoverageRate = demandRequests == 0
            ? 0m
            : Math.Round((decimal)withProposal * 100m / demandRequests, 2, MidpointRounding.AwayFromZero);

        var firstProposalSlaRate = demandRequests == 0
            ? 0m
            : Math.Round((decimal)withinSlaCount * 100m / demandRequests, 2, MidpointRounding.AwayFromZero);

        var medianMinutes = durationsMinutes.Count == 0
            ? (decimal?)null
            : ResolveMedian(durationsMinutes);

        var score = ResolveLiquidityScore(
            demandRequests: demandRequests,
            withProposal: withProposal,
            distinctProviders: distinctProviders.Count,
            medianFirstProposalMinutes: medianMinutes,
            proposalSlaMinutes: proposalSlaMinutes);

        return new AdminLiquidityScoreItemDto(
            Region: region,
            Category: category,
            DemandRequests: demandRequests,
            RequestsWithProposal: withProposal,
            RequestsWithoutProposal: withoutProposal,
            DistinctProviders: distinctProviders.Count,
            ProposalCoverageRatePercent: proposalCoverageRate,
            FirstProposalSlaRatePercent: firstProposalSlaRate,
            MedianFirstProposalMinutes: medianMinutes,
            LiquidityScore: score,
            LiquidityBand: ResolveBand(score));
    }

    private static IReadOnlyList<AdminLiquidityScoreHistoryPointDto> BuildHistory(
        IReadOnlyList<ServiceRequest> requests,
        IReadOnlyDictionary<Guid, List<Proposal>> proposalsByRequest,
        int proposalSlaMinutes)
    {
        return requests
            .GroupBy(request => request.CreatedAt.Date)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var dayRequests = group.ToList();
                var withProposal = 0;
                var durationsMinutes = new List<decimal>();
                var distinctProviders = new HashSet<Guid>();
                var withinSlaCount = 0;

                foreach (var request in dayRequests)
                {
                    if (!proposalsByRequest.TryGetValue(request.Id, out var proposals) || proposals.Count == 0)
                    {
                        continue;
                    }

                    withProposal++;
                    foreach (var providerId in proposals.Select(proposal => proposal.ProviderId))
                    {
                        distinctProviders.Add(providerId);
                    }

                    var elapsedMinutes = (decimal)(proposals[0].CreatedAt - request.CreatedAt).TotalMinutes;
                    durationsMinutes.Add(elapsedMinutes);
                    if (elapsedMinutes <= proposalSlaMinutes)
                    {
                        withinSlaCount++;
                    }
                }

                var coverageRate = dayRequests.Count == 0
                    ? 0m
                    : Math.Round((decimal)withProposal * 100m / dayRequests.Count, 2, MidpointRounding.AwayFromZero);

                var slaRate = dayRequests.Count == 0
                    ? 0m
                    : Math.Round((decimal)withinSlaCount * 100m / dayRequests.Count, 2, MidpointRounding.AwayFromZero);

                var medianMinutes = durationsMinutes.Count == 0 ? (decimal?)null : ResolveMedian(durationsMinutes);

                return new AdminLiquidityScoreHistoryPointDto(
                    BucketDateUtc: DateTime.SpecifyKind(group.Key, DateTimeKind.Utc),
                    DemandRequests: dayRequests.Count,
                    RequestsWithProposal: withProposal,
                    DistinctProviders: distinctProviders.Count,
                    ProposalCoverageRatePercent: coverageRate,
                    FirstProposalSlaRatePercent: slaRate,
                    LiquidityScore: ResolveLiquidityScore(
                        demandRequests: dayRequests.Count,
                        withProposal: withProposal,
                        distinctProviders: distinctProviders.Count,
                        medianFirstProposalMinutes: medianMinutes,
                        proposalSlaMinutes: proposalSlaMinutes));
            })
            .ToList();
    }

    private static IReadOnlyList<AdminGrowthAlertDto> BuildAlerts(
        IReadOnlyList<AdminLiquidityScoreItemDto> items,
        int requestCount)
    {
        var alerts = new List<AdminGrowthAlertDto>();
        if (requestCount == 0)
        {
            return alerts;
        }

        var criticalItems = items.Where(item => item.LiquidityBand == "critical").ToList();
        if (criticalItems.Count > 0)
        {
            alerts.Add(new AdminGrowthAlertDto(
                Code: "liquidity_critical_regions",
                Severity: "critical",
                Title: "Regioes/categorias em deficit critico de liquidez",
                Description: "Ha combinacoes de regiao/categoria com score critico e risco alto de pedidos sem proposta.",
                CurrentValue: criticalItems.Count,
                ThresholdValue: 1,
                Unit: " grupos"));
        }

        var averageScore = items.Count == 0
            ? 0m
            : Math.Round(items.Average(item => item.LiquidityScore), 2, MidpointRounding.AwayFromZero);

        if (averageScore < 45m)
        {
            alerts.Add(new AdminGrowthAlertDto(
                Code: "liquidity_average_score_critical",
                Severity: "critical",
                Title: "Score medio de liquidez abaixo do minimo",
                Description: "Liquidez geral abaixo da faixa segura. Recomenda-se acao combinada de captacao e velocidade de resposta.",
                CurrentValue: averageScore,
                ThresholdValue: 45m,
                Unit: " pts"));
        }
        else if (averageScore < 65m)
        {
            alerts.Add(new AdminGrowthAlertDto(
                Code: "liquidity_average_score_warning",
                Severity: "warning",
                Title: "Score medio de liquidez em atencao",
                Description: "Liquidez geral abaixo da meta recomendada para manter conversao estavel.",
                CurrentValue: averageScore,
                ThresholdValue: 65m,
                Unit: " pts"));
        }

        return alerts;
    }

    private static decimal ResolveLiquidityScore(
        int demandRequests,
        int withProposal,
        int distinctProviders,
        decimal? medianFirstProposalMinutes,
        int proposalSlaMinutes)
    {
        if (demandRequests <= 0)
        {
            return 0m;
        }

        var coverageFactor = (decimal)withProposal / demandRequests;
        var providerTarget = Math.Max(1m, demandRequests * 0.60m);
        var supplyFactor = Math.Min(1m, distinctProviders / providerTarget);
        var responseFactor = medianFirstProposalMinutes.HasValue
            ? Math.Max(0m, 1m - (medianFirstProposalMinutes.Value / proposalSlaMinutes))
            : 0m;

        var score = (coverageFactor * CoverageWeight) +
                    (supplyFactor * SupplyWeight) +
                    (responseFactor * ResponseWeight);

        return Math.Round(score * 100m, 2, MidpointRounding.AwayFromZero);
    }

    private static string ResolveBand(decimal score)
    {
        if (score < 40m)
        {
            return "critical";
        }

        if (score < 65m)
        {
            return "warning";
        }

        return "healthy";
    }

    private static bool MatchesCategory(ServiceRequest request, string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return true;
        }

        var normalized = category.Trim();
        if (ServiceCategoryExtensions.TryParseFlexible(normalized, out var parsedCategory)
            && request.Category == parsedCategory)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(request.CategoryDefinition?.Name)
            && request.CategoryDefinition.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return request.Category.ToPtBr().Contains(normalized, StringComparison.OrdinalIgnoreCase)
            || request.Category.ToString().Contains(normalized, StringComparison.OrdinalIgnoreCase);
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

    private static string ResolveCategoryLabel(ServiceRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.CategoryDefinition?.Name))
        {
            return request.CategoryDefinition.Name.Trim();
        }

        return request.Category.ToPtBr();
    }

    private static string NormalizeRegion(string? city)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            return "Nao informado";
        }

        return city.Trim();
    }

    private static (DateTime fromUtc, DateTime toUtc) NormalizeRange(DateTime? fromUtc, DateTime? toUtc)
    {
        var nowUtc = DateTime.UtcNow;
        var normalizedTo = toUtc?.ToUniversalTime() ?? nowUtc;
        var normalizedFrom = fromUtc?.ToUniversalTime() ?? normalizedTo.AddDays(-14);

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
