using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using System.Text.Json;

namespace ConsertaPraMim.Application.Services;

public class AdminGrowthService : IAdminGrowthService
{
    private readonly IUserRepository _userRepository;
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IProposalRepository _proposalRepository;
    private readonly IAdminAuditLogRepository? _adminAuditLogRepository;

    public AdminGrowthService(
        IUserRepository userRepository,
        IServiceRequestRepository serviceRequestRepository,
        IProposalRepository proposalRepository,
        IAdminAuditLogRepository? adminAuditLogRepository = null)
    {
        _userRepository = userRepository;
        _serviceRequestRepository = serviceRequestRepository;
        _proposalRepository = proposalRepository;
        _adminAuditLogRepository = adminAuditLogRepository;
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

    public async Task<AdminProviderReactivationSegmentsDto> GetProviderReactivationSegmentsAsync(
        AdminProviderReactivationSegmentsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var asOfUtc = (query.AsOfUtc ?? DateTime.UtcNow).ToUniversalTime();
        var warmFromDays = Math.Clamp(query.WarmFromDays, 1, 365);
        var coldFromDays = Math.Clamp(query.ColdFromDays, warmFromDays + 1, 365);
        var dormantFromDays = Math.Clamp(query.DormantFromDays, coldFromDays + 1, 730);
        var hibernatedFromDays = Math.Clamp(query.HibernatedFromDays, dormantFromDays + 1, 1460);
        var previewTake = Math.Clamp(query.PreviewTake, 10, 200);

        var providers = (await _userRepository.GetAllAsync())
            .Where(user =>
                user.Role == UserRole.Provider &&
                user.IsActive &&
                user.ProviderProfile != null)
            .ToList();

        if (providers.Count == 0)
        {
            return new AdminProviderReactivationSegmentsDto(
                AsOfUtc: asOfUtc,
                TotalProviders: 0,
                ActiveProviders: 0,
                InactiveProviders: 0,
                Segments: Array.Empty<AdminProviderReactivationSegmentBreakdownDto>(),
                Preview: Array.Empty<AdminProviderReactivationProviderPreviewDto>());
        }

        var providerIds = providers
            .Select(provider => provider.Id)
            .ToHashSet();

        var proposalsByProvider = (await _proposalRepository.GetAllAsync())
            .Where(proposal => !proposal.IsInvalidated)
            .Where(proposal => providerIds.Contains(proposal.ProviderId))
            .GroupBy(proposal => proposal.ProviderId)
            .ToDictionary(group => group.Key, group => group.Max(item => item.CreatedAt));

        var loginByProvider = await ResolveLastLoginByProviderAsync(providerIds, asOfUtc, cancellationToken);

        var providersWithActivity = providers
            .Select(provider =>
            {
                var lastProposalAtUtc = proposalsByProvider.TryGetValue(provider.Id, out var proposalAt)
                    ? proposalAt
                    : (DateTime?)null;
                var lastLoginAtUtc = loginByProvider.TryGetValue(provider.Id, out var loginAt)
                    ? loginAt
                    : (DateTime?)null;
                var lastActivityAtUtc = ResolveLastActivityUtc(lastProposalAtUtc, lastLoginAtUtc, provider.CreatedAt);
                var inactivityDays = Math.Max(0, (int)Math.Floor((asOfUtc - lastActivityAtUtc).TotalDays));
                var segment = ResolveInactivitySegment(inactivityDays, warmFromDays, coldFromDays, dormantFromDays, hibernatedFromDays);

                var category = ResolvePrimaryCategory(provider);
                var region = ResolveProviderRegion(provider);

                return new ProviderInactivitySnapshot(
                    Provider: provider,
                    LastActivityAtUtc: lastActivityAtUtc,
                    InactivityDays: inactivityDays,
                    SegmentCode: segment.Code,
                    SegmentLabel: segment.Label,
                    Category: category,
                    Region: region);
            })
            .ToList();

        var inactiveProviders = providersWithActivity.Where(item => !item.SegmentCode.Equals("active", StringComparison.OrdinalIgnoreCase)).ToList();
        var totalInactive = inactiveProviders.Count;

        var segmentBreakdown = inactiveProviders
            .GroupBy(item => item.SegmentCode, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var descriptor = DescribeSegment(group.Key, warmFromDays, coldFromDays, dormantFromDays, hibernatedFromDays);
                var orderedByVolume = group
                    .GroupBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(item => item.Count())
                    .FirstOrDefault();
                var topCategory = orderedByVolume?.Key;
                var topRegion = group
                    .GroupBy(item => item.Region, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(item => item.Count())
                    .Select(item => item.Key)
                    .FirstOrDefault();

                var providersCount = group.Count();
                var share = totalInactive == 0
                    ? 0m
                    : decimal.Round((decimal)providersCount * 100m / totalInactive, 2, MidpointRounding.AwayFromZero);

                return new AdminProviderReactivationSegmentBreakdownDto(
                    SegmentCode: descriptor.Code,
                    SegmentLabel: descriptor.Label,
                    MinDaysInclusive: descriptor.MinDaysInclusive,
                    MaxDaysInclusive: descriptor.MaxDaysInclusive,
                    Providers: providersCount,
                    ProvidersSharePercent: share,
                    DistinctCategories: group.Select(item => item.Category).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    DistinctRegions: group.Select(item => item.Region).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    TopCategory: topCategory,
                    TopRegion: topRegion);
            })
            .OrderByDescending(item => item.MinDaysInclusive)
            .ThenBy(item => item.SegmentCode)
            .ToList();

        var preview = inactiveProviders
            .OrderByDescending(item => item.InactivityDays)
            .ThenBy(item => item.Provider.Name)
            .Take(previewTake)
            .Select(item => new AdminProviderReactivationProviderPreviewDto(
                ProviderId: item.Provider.Id,
                ProviderName: string.IsNullOrWhiteSpace(item.Provider.Name) ? "Prestador sem nome" : item.Provider.Name.Trim(),
                ProviderEmail: item.Provider.Email,
                InactivityDays: item.InactivityDays,
                LastActivityAtUtc: item.LastActivityAtUtc,
                SegmentCode: item.SegmentCode,
                SegmentLabel: item.SegmentLabel,
                Category: item.Category,
                Region: item.Region))
            .ToList();

        return new AdminProviderReactivationSegmentsDto(
            AsOfUtc: asOfUtc,
            TotalProviders: providersWithActivity.Count,
            ActiveProviders: providersWithActivity.Count(item => item.SegmentCode.Equals("active", StringComparison.OrdinalIgnoreCase)),
            InactiveProviders: totalInactive,
            Segments: segmentBreakdown,
            Preview: preview);
    }

    public async Task<AdminProviderReactivationCampaignRunResultDto> RunProviderReactivationCampaignAsync(
        AdminProviderReactivationCampaignRunRequestDto request,
        Guid actorUserId,
        string actorEmail,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var nowUtc = DateTime.UtcNow;
        var cadenceHours = Math.Clamp(request.CadenceHours, 1, 168);
        var maxRecipients = Math.Clamp(request.MaxRecipients, 1, 500);
        var normalizedSegmentCode = string.IsNullOrWhiteSpace(request.SegmentCode)
            ? null
            : request.SegmentCode.Trim().ToLowerInvariant();

        DateTime? previousCampaignAtUtc = null;
        if (_adminAuditLogRepository != null)
        {
            var previousRuns = await _adminAuditLogRepository.GetByTargetAndPeriodAsync(
                targetType: "ProviderReactivationCampaign",
                fromUtc: nowUtc.AddDays(-30),
                toUtc: nowUtc,
                action: "campaign_run_completed",
                take: 1);

            previousCampaignAtUtc = previousRuns.FirstOrDefault()?.CreatedAt;
        }

        if (!request.ForceRun &&
            previousCampaignAtUtc.HasValue &&
            nowUtc < previousCampaignAtUtc.Value.AddHours(cadenceHours))
        {
            var remaining = previousCampaignAtUtc.Value.AddHours(cadenceHours) - nowUtc;
            return new AdminProviderReactivationCampaignRunResultDto(
                CampaignId: Guid.Empty,
                RequestedAtUtc: nowUtc,
                Executed: false,
                Status: "skipped_cadence",
                Message: $"Campanha bloqueada por cadencia. Aguarde {Math.Ceiling(remaining.TotalHours)}h ou use ForceRun.",
                CadenceHours: cadenceHours,
                ForceRun: request.ForceRun,
                SelectedProviders: 0,
                SegmentCode: normalizedSegmentCode,
                PreviousCampaignAtUtc: previousCampaignAtUtc,
                Recipients: Array.Empty<AdminProviderReactivationProviderPreviewDto>());
        }

        var segments = await GetProviderReactivationSegmentsAsync(
            new AdminProviderReactivationSegmentsQueryDto(
                AsOfUtc: request.AsOfUtc,
                WarmFromDays: 7,
                ColdFromDays: 15,
                DormantFromDays: 31,
                HibernatedFromDays: 61,
                PreviewTake: Math.Min(maxRecipients, 200)),
            cancellationToken);

        var recipients = segments.Preview
            .Where(provider =>
                string.IsNullOrWhiteSpace(normalizedSegmentCode) ||
                provider.SegmentCode.Equals(normalizedSegmentCode, StringComparison.OrdinalIgnoreCase))
            .Take(maxRecipients)
            .ToList();

        var campaignId = Guid.NewGuid();
        await RegisterCampaignAuditAsync(
            actorUserId,
            actorEmail,
            "campaign_run_completed",
            campaignId,
            new
            {
                cadenceHours,
                forceRun = request.ForceRun,
                maxRecipients,
                segmentCode = normalizedSegmentCode,
                selectedProviders = recipients.Count,
                providerIds = recipients.Select(item => item.ProviderId.ToString("N")).ToArray()
            },
            cancellationToken);

        return new AdminProviderReactivationCampaignRunResultDto(
            CampaignId: campaignId,
            RequestedAtUtc: nowUtc,
            Executed: true,
            Status: recipients.Count == 0 ? "completed_without_recipients" : "completed",
            Message: recipients.Count == 0
                ? "Campanha executada sem prestadores elegiveis para o segmento informado."
                : $"Campanha preparada com {recipients.Count} prestador(es) para reativacao.",
            CadenceHours: cadenceHours,
            ForceRun: request.ForceRun,
            SelectedProviders: recipients.Count,
            SegmentCode: normalizedSegmentCode,
            PreviousCampaignAtUtc: previousCampaignAtUtc,
            Recipients: recipients);
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

    private async Task<Dictionary<Guid, DateTime>> ResolveLastLoginByProviderAsync(
        HashSet<Guid> providerIds,
        DateTime asOfUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_adminAuditLogRepository == null || providerIds.Count == 0)
        {
            return new Dictionary<Guid, DateTime>();
        }

        var logs = await _adminAuditLogRepository.GetByTargetAndPeriodAsync(
            targetType: "UserAuth",
            fromUtc: asOfUtc.AddDays(-365),
            toUtc: asOfUtc,
            action: "user_login",
            take: 20000);

        return logs
            .Where(log => providerIds.Contains(log.ActorUserId))
            .GroupBy(log => log.ActorUserId)
            .ToDictionary(
                group => group.Key,
                group => group.Max(item => item.CreatedAt));
    }

    private static DateTime ResolveLastActivityUtc(
        DateTime? lastProposalAtUtc,
        DateTime? lastLoginAtUtc,
        DateTime fallbackCreatedAtUtc)
    {
        var candidates = new List<DateTime>(3) { fallbackCreatedAtUtc };
        if (lastProposalAtUtc.HasValue)
        {
            candidates.Add(lastProposalAtUtc.Value);
        }

        if (lastLoginAtUtc.HasValue)
        {
            candidates.Add(lastLoginAtUtc.Value);
        }

        return candidates.Max();
    }

    private static (string Code, string Label) ResolveInactivitySegment(
        int inactivityDays,
        int warmFromDays,
        int coldFromDays,
        int dormantFromDays,
        int hibernatedFromDays)
    {
        if (inactivityDays < warmFromDays)
        {
            return ("active", "Ativo");
        }

        if (inactivityDays < coldFromDays)
        {
            return ("warm", "Atencao");
        }

        if (inactivityDays < dormantFromDays)
        {
            return ("cold", "Frio");
        }

        if (inactivityDays < hibernatedFromDays)
        {
            return ("dormant", "Dormente");
        }

        return ("hibernated", "Hibernado");
    }

    private static InactivitySegmentDescriptor DescribeSegment(
        string segmentCode,
        int warmFromDays,
        int coldFromDays,
        int dormantFromDays,
        int hibernatedFromDays)
    {
        return segmentCode.Trim().ToLowerInvariant() switch
        {
            "warm" => new InactivitySegmentDescriptor("warm", "Atencao", warmFromDays, coldFromDays - 1),
            "cold" => new InactivitySegmentDescriptor("cold", "Frio", coldFromDays, dormantFromDays - 1),
            "dormant" => new InactivitySegmentDescriptor("dormant", "Dormente", dormantFromDays, hibernatedFromDays - 1),
            "hibernated" => new InactivitySegmentDescriptor("hibernated", "Hibernado", hibernatedFromDays, null),
            _ => new InactivitySegmentDescriptor("active", "Ativo", 0, warmFromDays - 1)
        };
    }

    private static string ResolvePrimaryCategory(User provider)
    {
        var category = provider.ProviderProfile?.Categories.FirstOrDefault();
        return category?.ToPtBr() ?? "Sem categoria";
    }

    private static string ResolveProviderRegion(User provider)
    {
        var baseZipCode = provider.ProviderProfile?.BaseZipCode?.Trim();
        if (string.IsNullOrWhiteSpace(baseZipCode))
        {
            return "Sem regiao";
        }

        var digits = new string(baseZipCode.Where(char.IsDigit).ToArray());
        if (digits.Length >= 5)
        {
            return $"CEP {digits[..5]}";
        }

        return $"CEP {digits}";
    }

    private async Task RegisterCampaignAuditAsync(
        Guid actorUserId,
        string actorEmail,
        string action,
        Guid campaignId,
        object metadata,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_adminAuditLogRepository == null)
        {
            return;
        }

        await _adminAuditLogRepository.AddAsync(new AdminAuditLog
        {
            ActorUserId = actorUserId == Guid.Empty ? Guid.Empty : actorUserId,
            ActorEmail = string.IsNullOrWhiteSpace(actorEmail) ? "system@consertapramim.local" : actorEmail.Trim(),
            Action = action,
            TargetType = "ProviderReactivationCampaign",
            TargetId = campaignId,
            Metadata = JsonSerializer.Serialize(metadata)
        });
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

    private sealed record ProviderInactivitySnapshot(
        User Provider,
        DateTime LastActivityAtUtc,
        int InactivityDays,
        string SegmentCode,
        string SegmentLabel,
        string Category,
        string Region);

    private sealed record InactivitySegmentDescriptor(
        string Code,
        string Label,
        int MinDaysInclusive,
        int? MaxDaysInclusive);
}
