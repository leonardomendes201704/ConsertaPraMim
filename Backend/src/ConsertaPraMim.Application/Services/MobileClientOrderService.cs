using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class MobileClientOrderService : IMobileClientOrderService
{
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IProposalService _proposalService;

    public MobileClientOrderService(
        IServiceRequestRepository serviceRequestRepository,
        IProposalService proposalService)
    {
        _serviceRequestRepository = serviceRequestRepository;
        _proposalService = proposalService;
    }

    public async Task<MobileClientOrdersResponseDto> GetMyOrdersAsync(Guid clientUserId, int takePerBucket = 100)
    {
        var normalizedTake = Math.Clamp(takePerBucket, 1, 300);
        var requests = await _serviceRequestRepository.GetByClientIdAsync(clientUserId);

        var projected = requests
            .OrderByDescending(request => request.CreatedAt)
            .Select(request => new
            {
                Request = request,
                Item = MapToMobileOrderItem(request)
            })
            .ToList();

        var openOrders = projected
            .Where(item => !IsFinalizedStatus(item.Request.Status))
            .Take(normalizedTake)
            .Select(item => item.Item)
            .ToList();

        var finalizedOrders = projected
            .Where(item => IsFinalizedStatus(item.Request.Status))
            .Take(normalizedTake)
            .Select(item => item.Item)
            .ToList();

        return new MobileClientOrdersResponseDto(
            openOrders,
            finalizedOrders,
            openOrders.Count,
            finalizedOrders.Count,
            openOrders.Count + finalizedOrders.Count);
    }

    public async Task<MobileClientOrderDetailsResponseDto?> GetOrderDetailsAsync(Guid clientUserId, Guid orderId)
    {
        var request = await _serviceRequestRepository.GetByIdAsync(orderId);
        if (request == null || request.ClientId != clientUserId)
        {
            return null;
        }

        var order = MapToMobileOrderItem(request);
        var flowSteps = BuildFlowSteps(request);
        var timeline = BuildTimeline(request);

        return new MobileClientOrderDetailsResponseDto(order, flowSteps, timeline);
    }

    public async Task<MobileClientOrderProposalDetailsResponseDto?> GetOrderProposalDetailsAsync(
        Guid clientUserId,
        Guid orderId,
        Guid proposalId)
    {
        var request = await _serviceRequestRepository.GetByIdAsync(orderId);
        if (request == null || request.ClientId != clientUserId)
        {
            return null;
        }

        var proposal = request.Proposals.FirstOrDefault(item => item.Id == proposalId);
        if (proposal == null)
        {
            return null;
        }

        var statusLabel = ResolveProposalStatusLabel(proposal);
        var providerName = proposal.Provider?.Name ?? "Prestador";
        var currentAppointment = ResolveCurrentAppointment(request, proposal, providerName);

        return new MobileClientOrderProposalDetailsResponseDto(
            MapToMobileOrderItem(request),
            new MobileClientOrderProposalDetailsDto(
                proposal.Id,
                request.Id,
                proposal.ProviderId,
                providerName,
                proposal.EstimatedValue,
                NormalizeOptionalText(proposal.Message),
                proposal.Accepted,
                proposal.IsInvalidated,
                statusLabel,
                proposal.CreatedAt,
                proposal.EstimatedLeadTimeHours,
                proposal.WarrantyDays),
            currentAppointment);
    }

    public async Task<MobileClientProposalComparisonResponseDto?> GetOrderProposalComparisonAsync(
        Guid clientUserId,
        Guid orderId,
        string? sortBy = null)
    {
        var request = await _serviceRequestRepository.GetByIdAsync(orderId);
        if (request == null || request.ClientId != clientUserId)
        {
            return null;
        }

        var normalizedSortBy = NormalizeComparisonSortBy(sortBy);
        var orderedProposals = request.Proposals
            .OrderBy(proposal => proposal.CreatedAt)
            .ToList();

        var comparisonReference = BuildComparisonReference(orderedProposals);
        var comparisonItems = orderedProposals
            .Select(proposal => MapComparisonItem(request, proposal, comparisonReference))
            .ToList();

        var sortedItems = SortComparisonItems(comparisonItems, normalizedSortBy);

        var prices = orderedProposals
            .Where(proposal => !proposal.IsInvalidated && proposal.EstimatedValue.HasValue)
            .Select(proposal => proposal.EstimatedValue!.Value)
            .ToList();

        var leadTimes = orderedProposals
            .Where(proposal => !proposal.IsInvalidated && proposal.EstimatedLeadTimeHours.HasValue)
            .Select(proposal => proposal.EstimatedLeadTimeHours!.Value)
            .ToList();

        var warranties = orderedProposals
            .Where(proposal => !proposal.IsInvalidated && proposal.WarrantyDays.HasValue)
            .Select(proposal => proposal.WarrantyDays!.Value)
            .ToList();

        var summary = new MobileClientProposalComparisonSummaryDto(
            TotalProposals: orderedProposals.Count,
            LowestPrice: prices.Count > 0 ? prices.Min() : null,
            HighestPrice: prices.Count > 0 ? prices.Max() : null,
            FastestLeadTimeHours: leadTimes.Count > 0 ? leadTimes.Min() : null,
            HighestWarrantyDays: warranties.Count > 0 ? warranties.Max() : null);

        return new MobileClientProposalComparisonResponseDto(
            orderId,
            ExperimentGroup: "comparison_default",
            SortBy: normalizedSortBy,
            AvailableSortOptions: MobileClientProposalComparisonSortBy.All,
            Summary: summary,
            Proposals: sortedItems);
    }

    public async Task<MobileClientAcceptProposalResponseDto?> AcceptProposalAsync(
        Guid clientUserId,
        Guid orderId,
        Guid proposalId)
    {
        var request = await _serviceRequestRepository.GetByIdAsync(orderId);
        if (request == null || request.ClientId != clientUserId)
        {
            return null;
        }

        var proposal = request.Proposals.FirstOrDefault(item => item.Id == proposalId);
        if (proposal == null || proposal.IsInvalidated)
        {
            return null;
        }

        var accepted = proposal.Accepted || await _proposalService.AcceptAsync(proposalId, clientUserId);
        if (!accepted)
        {
            return null;
        }

        var updatedRequest = await _serviceRequestRepository.GetByIdAsync(orderId);
        if (updatedRequest == null || updatedRequest.ClientId != clientUserId)
        {
            return null;
        }

        var updatedProposal = updatedRequest.Proposals.FirstOrDefault(item => item.Id == proposalId);
        if (updatedProposal == null)
        {
            return null;
        }

        var providerName = updatedProposal.Provider?.Name ?? "Prestador";

        return new MobileClientAcceptProposalResponseDto(
            MapToMobileOrderItem(updatedRequest),
            new MobileClientOrderProposalDetailsDto(
                updatedProposal.Id,
                updatedRequest.Id,
                updatedProposal.ProviderId,
                providerName,
                updatedProposal.EstimatedValue,
                NormalizeOptionalText(updatedProposal.Message),
                updatedProposal.Accepted,
                updatedProposal.IsInvalidated,
                ResolveProposalStatusLabel(updatedProposal),
                updatedProposal.CreatedAt,
                updatedProposal.EstimatedLeadTimeHours,
                updatedProposal.WarrantyDays),
            "Proposta aceita com sucesso! O prestador foi notificado.");
    }

    private static IReadOnlyList<MobileClientOrderFlowStepDto> BuildFlowSteps(ServiceRequest request)
    {
        var currentStep = DetermineCurrentFlowStep(request);
        var finalStepTitle = request.Status == ServiceRequestStatus.Canceled
            ? "Pedido cancelado"
            : "Servico finalizado";

        var steps = new List<(int Step, string Title)>
        {
            (1, "Pedido criado"),
            (2, "Propostas recebidas"),
            (3, "Agendamento confirmado"),
            (4, "Servico em andamento"),
            (5, "Aguardando confirmacao final"),
            (6, finalStepTitle)
        };

        return steps
            .Select(step => new MobileClientOrderFlowStepDto(
                step.Step,
                step.Title,
                Completed: step.Step < currentStep,
                Current: step.Step == currentStep))
            .ToList();
    }

    private static int DetermineCurrentFlowStep(ServiceRequest request)
    {
        return request.Status switch
        {
            ServiceRequestStatus.Created => request.Proposals.Any() ? 2 : 1,
            ServiceRequestStatus.Matching => request.Proposals.Any() ? 2 : 1,
            ServiceRequestStatus.Scheduled => 3,
            ServiceRequestStatus.InProgress => 4,
            ServiceRequestStatus.PendingClientCompletionAcceptance => 5,
            ServiceRequestStatus.Completed => 6,
            ServiceRequestStatus.Validated => 6,
            ServiceRequestStatus.Canceled => 6,
            _ => 1
        };
    }

    private static IReadOnlyList<MobileClientOrderTimelineEventDto> BuildTimeline(ServiceRequest request)
    {
        var events = new List<MobileClientOrderTimelineEventDto>
        {
            new(
                "request_created",
                "Pedido criado",
                "Seu pedido foi registrado e entrou na fila de atendimento.",
                request.CreatedAt)
        };

        foreach (var proposal in request.Proposals.OrderBy(proposal => proposal.CreatedAt))
        {
            var providerName = proposal.Provider?.Name ?? "Prestador";
            var hasValue = proposal.EstimatedValue.HasValue;
            var valueText = hasValue ? $" Valor estimado: R$ {proposal.EstimatedValue:0.00}." : string.Empty;

            if (proposal.Accepted)
            {
                events.Add(new MobileClientOrderTimelineEventDto(
                    "proposal_accepted",
                    "Proposta aceita",
                    $"Voce aceitou a proposta de {providerName}.{valueText}",
                    proposal.CreatedAt,
                    "proposal",
                    proposal.Id));
            }
            else
            {
                events.Add(new MobileClientOrderTimelineEventDto(
                    "proposal_received",
                    "Proposta recebida",
                    $"Nova proposta enviada por {providerName}.{valueText}",
                    proposal.CreatedAt,
                    "proposal",
                    proposal.Id));
            }
        }

        foreach (var appointment in request.Appointments.OrderBy(appointment => appointment.CreatedAt))
        {
            var providerName = appointment.Provider?.Name ?? "Prestador";
            var windowText = $"{appointment.WindowStartUtc:dd/MM/yyyy HH:mm} - {appointment.WindowEndUtc:HH:mm}";

            events.Add(new MobileClientOrderTimelineEventDto(
                "appointment_requested",
                "Agendamento solicitado",
                $"{providerName} sugeriu a janela {windowText}.",
                appointment.CreatedAt));

            var orderedHistory = appointment.History
                .OrderBy(history => history.OccurredAtUtc)
                .ToList();

            foreach (var history in orderedHistory)
            {
                var title = MapAppointmentHistoryTitle(history.NewStatus);
                var description = string.IsNullOrWhiteSpace(history.Reason)
                    ? "Atualizacao operacional do agendamento."
                    : history.Reason.Trim();

                events.Add(new MobileClientOrderTimelineEventDto(
                    $"appointment_{history.NewStatus.ToString().ToLowerInvariant()}",
                    title,
                    description,
                    history.OccurredAtUtc));
            }

            if (!orderedHistory.Any() && appointment.ConfirmedAtUtc.HasValue)
            {
                events.Add(new MobileClientOrderTimelineEventDto(
                    "appointment_confirmed",
                    "Agendamento confirmado",
                    "O prestador confirmou a visita.",
                    appointment.ConfirmedAtUtc.Value));
            }
        }

        foreach (var review in request.Reviews
                     .Where(review => review.ReviewerRole == UserRole.Client)
                     .OrderBy(review => review.CreatedAt))
        {
            events.Add(new MobileClientOrderTimelineEventDto(
                "client_review_submitted",
                "Avaliacao enviada",
                "Sua avaliacao foi registrada para este atendimento.",
                review.CreatedAt));
        }

        if (request.Status == ServiceRequestStatus.Canceled &&
            events.All(item => !item.EventCode.Contains("cancel", StringComparison.OrdinalIgnoreCase)))
        {
            events.Add(new MobileClientOrderTimelineEventDto(
                "request_canceled",
                "Pedido cancelado",
                "Este pedido foi cancelado.",
                ResolveFallbackEventDate(request)));
        }

        if ((request.Status == ServiceRequestStatus.Completed || request.Status == ServiceRequestStatus.Validated) &&
            events.All(item => !item.EventCode.Contains("completed", StringComparison.OrdinalIgnoreCase)))
        {
            events.Add(new MobileClientOrderTimelineEventDto(
                "request_completed",
                "Servico finalizado",
                "O fluxo principal do pedido foi concluido.",
                ResolveFallbackEventDate(request)));
        }

        return events
            .OrderBy(item => item.OccurredAtUtc)
            .ToList();
    }

    private static DateTime ResolveFallbackEventDate(ServiceRequest request)
    {
        var latestAppointmentTimestamp = request.Appointments
            .SelectMany(appointment => new DateTime?[]
            {
                appointment.CompletedAtUtc,
                appointment.CancelledAtUtc,
                appointment.ConfirmedAtUtc,
                appointment.StartedAtUtc,
                appointment.CreatedAt
            })
            .Where(date => date.HasValue)
            .Select(date => date!.Value)
            .DefaultIfEmpty(request.CreatedAt)
            .Max();

        return latestAppointmentTimestamp;
    }

    private static string MapAppointmentHistoryTitle(ServiceAppointmentStatus status)
    {
        return status switch
        {
            ServiceAppointmentStatus.PendingProviderConfirmation => "Aguardando confirmacao do prestador",
            ServiceAppointmentStatus.Confirmed => "Agendamento confirmado",
            ServiceAppointmentStatus.RejectedByProvider => "Agendamento rejeitado",
            ServiceAppointmentStatus.ExpiredWithoutProviderAction => "Agendamento expirou",
            ServiceAppointmentStatus.RescheduleRequestedByClient => "Reagendamento solicitado pelo cliente",
            ServiceAppointmentStatus.RescheduleRequestedByProvider => "Reagendamento solicitado pelo prestador",
            ServiceAppointmentStatus.RescheduleConfirmed => "Reagendamento confirmado",
            ServiceAppointmentStatus.CancelledByClient => "Agendamento cancelado pelo cliente",
            ServiceAppointmentStatus.CancelledByProvider => "Agendamento cancelado pelo prestador",
            ServiceAppointmentStatus.Completed => "Visita concluida",
            ServiceAppointmentStatus.Arrived => "Prestador chegou ao local",
            ServiceAppointmentStatus.InProgress => "Servico em andamento",
            _ => "Atualizacao do agendamento"
        };
    }

    private static MobileClientOrderItemDto MapToMobileOrderItem(ServiceRequest request)
    {
        var category = ResolveCategoryDisplay(request);
        var normalizedDescription = request.Description?.Trim();

        return new MobileClientOrderItemDto(
            request.Id,
            ResolveTitle(category, normalizedDescription),
            MapStatusToMobileStatus(request.Status),
            category,
            request.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
            ResolveCategoryIcon(category),
            normalizedDescription,
            request.Proposals.Count(proposal => !proposal.IsInvalidated));
    }

    private static bool IsFinalizedStatus(ServiceRequestStatus status)
    {
        return status == ServiceRequestStatus.Completed ||
               status == ServiceRequestStatus.Validated ||
               status == ServiceRequestStatus.Canceled;
    }

    private static string MapStatusToMobileStatus(ServiceRequestStatus status)
    {
        return status switch
        {
            ServiceRequestStatus.InProgress => "EM_ANDAMENTO",
            ServiceRequestStatus.Completed => "CONCLUIDO",
            ServiceRequestStatus.Validated => "CONCLUIDO",
            ServiceRequestStatus.Canceled => "CANCELADO",
            _ => "AGUARDANDO"
        };
    }

    private static string ResolveTitle(string category, string? description)
    {
        if (!string.IsNullOrWhiteSpace(description))
        {
            var compact = description.Trim();
            if (compact.Length <= 48)
            {
                return compact;
            }

            return compact[..45].TrimEnd() + "...";
        }

        return $"Pedido de {category}";
    }

    private static string ResolveCategoryDisplay(ServiceRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.CategoryDefinition?.Name)
            ? request.CategoryDefinition!.Name
            : request.Category.ToString();
    }

    private static string ResolveCategoryIcon(string categoryName)
    {
        var normalized = categoryName.Trim().ToLowerInvariant();

        if (normalized.Contains("eletric"))
        {
            return "bolt";
        }

        if (normalized.Contains("hidraul") || normalized.Contains("encan"))
        {
            return "water_drop";
        }

        if (normalized.Contains("pintur"))
        {
            return "format_paint";
        }

        if (normalized.Contains("montag") || normalized.Contains("marcen"))
        {
            return "construction";
        }

        if (normalized.Contains("limpez"))
        {
            return "cleaning_services";
        }

        if (normalized.Contains("alvenar"))
        {
            return "home_repair_service";
        }

        if (normalized.Contains("eletron"))
        {
            return "memory";
        }

        if (normalized.Contains("eletrodom"))
        {
            return "kitchen";
        }

        if (normalized.Contains("jardin"))
        {
            return "yard";
        }

        return "build_circle";
    }

    private static string NormalizeComparisonSortBy(string? sortBy)
    {
        var normalized = sortBy?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(normalized) && MobileClientProposalComparisonSortBy.All.Contains(normalized))
        {
            return normalized;
        }

        return MobileClientProposalComparisonSortBy.BestScore;
    }

    private static ProposalComparisonReference BuildComparisonReference(IReadOnlyList<Proposal> proposals)
    {
        var validProposals = proposals.Where(proposal => !proposal.IsInvalidated).ToList();
        var prices = validProposals
            .Where(proposal => proposal.EstimatedValue.HasValue)
            .Select(proposal => proposal.EstimatedValue!.Value)
            .ToList();
        var leadTimes = validProposals
            .Where(proposal => proposal.EstimatedLeadTimeHours.HasValue)
            .Select(proposal => proposal.EstimatedLeadTimeHours!.Value)
            .ToList();
        var warranties = validProposals
            .Where(proposal => proposal.WarrantyDays.HasValue)
            .Select(proposal => proposal.WarrantyDays!.Value)
            .ToList();
        var maxReviewCount = validProposals
            .Select(proposal => proposal.Provider?.ProviderProfile?.ReviewCount ?? 0)
            .DefaultIfEmpty(0)
            .Max();

        return new ProposalComparisonReference(
            LowestPrice: prices.Count > 0 ? prices.Min() : null,
            FastestLeadTimeHours: leadTimes.Count > 0 ? leadTimes.Min() : null,
            HighestWarrantyDays: warranties.Count > 0 ? warranties.Max() : null,
            MaxReviewCount: maxReviewCount);
    }

    private static MobileClientProposalComparisonItemDto MapComparisonItem(
        ServiceRequest request,
        Proposal proposal,
        ProposalComparisonReference reference)
    {
        var providerProfile = proposal.Provider?.ProviderProfile;
        var providerName = proposal.Provider?.Name ?? "Prestador";
        var providerRating = providerProfile?.Rating ?? 0;
        var providerReviewCount = providerProfile?.ReviewCount ?? 0;
        var providerCompletedServices = providerReviewCount;
        var responseTimeMinutes = Math.Max(1, (int)Math.Round((proposal.CreatedAt - request.CreatedAt).TotalMinutes));
        var comparisonScore = CalculateComparisonScore(
            proposal,
            reference,
            providerRating,
            providerReviewCount);

        return new MobileClientProposalComparisonItemDto(
            proposal.Id,
            request.Id,
            proposal.ProviderId,
            providerName,
            proposal.EstimatedValue,
            proposal.EstimatedLeadTimeHours,
            proposal.WarrantyDays,
            providerRating,
            providerReviewCount,
            providerCompletedServices,
            responseTimeMinutes,
            proposal.Accepted,
            proposal.IsInvalidated,
            ResolveProposalStatusLabel(proposal),
            proposal.CreatedAt,
            comparisonScore);
    }

    private static decimal CalculateComparisonScore(
        Proposal proposal,
        ProposalComparisonReference reference,
        double providerRating,
        int providerReviewCount)
    {
        if (proposal.IsInvalidated)
        {
            return 0m;
        }

        var priceComponent = 45d;
        if (proposal.EstimatedValue.HasValue &&
            reference.LowestPrice.HasValue &&
            proposal.EstimatedValue.Value > 0)
        {
            var ratio = (double)(reference.LowestPrice.Value / proposal.EstimatedValue.Value) * 100d;
            priceComponent = Math.Clamp(ratio, 20d, 100d);
        }

        var leadTimeComponent = 45d;
        if (proposal.EstimatedLeadTimeHours.HasValue &&
            reference.FastestLeadTimeHours.HasValue &&
            proposal.EstimatedLeadTimeHours.Value > 0)
        {
            var ratio = (double)reference.FastestLeadTimeHours.Value / proposal.EstimatedLeadTimeHours.Value * 100d;
            leadTimeComponent = Math.Clamp(ratio, 20d, 100d);
        }

        var warrantyComponent = 40d;
        if (proposal.WarrantyDays.HasValue)
        {
            if (reference.HighestWarrantyDays.HasValue && reference.HighestWarrantyDays.Value > 0)
            {
                var ratio = (double)proposal.WarrantyDays.Value / reference.HighestWarrantyDays.Value * 100d;
                warrantyComponent = Math.Clamp(ratio, 10d, 100d);
            }
            else
            {
                warrantyComponent = 60d;
            }
        }

        var ratingComponent = providerRating > 0
            ? Math.Clamp(providerRating / 5d * 100d, 0d, 100d)
            : 35d;

        var historyComponent = 35d;
        if (reference.MaxReviewCount > 0)
        {
            historyComponent = Math.Clamp((double)providerReviewCount / reference.MaxReviewCount * 100d, 0d, 100d);
        }
        else if (providerReviewCount > 0)
        {
            historyComponent = 70d;
        }

        var weightedScore =
            (priceComponent * 0.35d) +
            (leadTimeComponent * 0.20d) +
            (warrantyComponent * 0.15d) +
            (ratingComponent * 0.20d) +
            (historyComponent * 0.10d);

        if (proposal.Accepted)
        {
            weightedScore += 5d;
        }

        return decimal.Round((decimal)Math.Clamp(weightedScore, 0d, 100d), 2, MidpointRounding.AwayFromZero);
    }

    private static IReadOnlyList<MobileClientProposalComparisonItemDto> SortComparisonItems(
        IReadOnlyList<MobileClientProposalComparisonItemDto> items,
        string sortBy)
    {
        return sortBy switch
        {
            MobileClientProposalComparisonSortBy.LowestPrice => items
                .OrderBy(item => item.Invalidated)
                .ThenBy(item => item.EstimatedValue ?? decimal.MaxValue)
                .ThenBy(item => item.EstimatedLeadTimeHours ?? int.MaxValue)
                .ThenByDescending(item => item.ProviderRating)
                .ThenBy(item => item.SentAtUtc)
                .ToList(),

            MobileClientProposalComparisonSortBy.FastestLeadTime => items
                .OrderBy(item => item.Invalidated)
                .ThenBy(item => item.EstimatedLeadTimeHours ?? int.MaxValue)
                .ThenBy(item => item.EstimatedValue ?? decimal.MaxValue)
                .ThenByDescending(item => item.ProviderRating)
                .ThenBy(item => item.SentAtUtc)
                .ToList(),

            MobileClientProposalComparisonSortBy.BestRating => items
                .OrderBy(item => item.Invalidated)
                .ThenByDescending(item => item.ProviderRating)
                .ThenByDescending(item => item.ProviderReviewCount)
                .ThenBy(item => item.EstimatedValue ?? decimal.MaxValue)
                .ThenBy(item => item.SentAtUtc)
                .ToList(),

            MobileClientProposalComparisonSortBy.HighestWarranty => items
                .OrderBy(item => item.Invalidated)
                .ThenByDescending(item => item.WarrantyDays ?? int.MinValue)
                .ThenByDescending(item => item.ProviderRating)
                .ThenBy(item => item.EstimatedValue ?? decimal.MaxValue)
                .ThenBy(item => item.SentAtUtc)
                .ToList(),

            _ => items
                .OrderBy(item => item.Invalidated)
                .ThenByDescending(item => item.ComparisonScore)
                .ThenBy(item => item.EstimatedValue ?? decimal.MaxValue)
                .ThenBy(item => item.EstimatedLeadTimeHours ?? int.MaxValue)
                .ThenBy(item => item.SentAtUtc)
                .ToList()
        };
    }

    private static string ResolveProposalStatusLabel(Proposal proposal)
    {
        if (proposal.IsInvalidated)
        {
            return "Invalidada";
        }

        if (proposal.Accepted)
        {
            return "Aceita";
        }

        return "Recebida";
    }

    private static MobileClientOrderProposalAppointmentDto? ResolveCurrentAppointment(
        ServiceRequest request,
        Proposal proposal,
        string providerName)
    {
        var appointment = request.Appointments
            .Where(item => item.ProviderId == proposal.ProviderId)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault();

        if (appointment == null)
        {
            return null;
        }

        var resolvedProviderName = appointment.Provider?.Name ?? providerName;
        return new MobileClientOrderProposalAppointmentDto(
            appointment.Id,
            request.Id,
            proposal.Id,
            appointment.ProviderId,
            resolvedProviderName,
            appointment.Status.ToString(),
            ResolveAppointmentStatusLabel(appointment.Status),
            appointment.WindowStartUtc,
            appointment.WindowEndUtc,
            appointment.CreatedAt,
            appointment.UpdatedAt);
    }

    private static string ResolveAppointmentStatusLabel(ServiceAppointmentStatus status)
    {
        return status switch
        {
            ServiceAppointmentStatus.PendingProviderConfirmation => "Aguardando confirmacao do prestador",
            ServiceAppointmentStatus.Confirmed => "Confirmado",
            ServiceAppointmentStatus.RejectedByProvider => "Recusado pelo prestador",
            ServiceAppointmentStatus.ExpiredWithoutProviderAction => "Expirado sem confirmacao",
            ServiceAppointmentStatus.RescheduleRequestedByClient => "Reagendamento solicitado pelo cliente",
            ServiceAppointmentStatus.RescheduleRequestedByProvider => "Reagendamento solicitado pelo prestador",
            ServiceAppointmentStatus.RescheduleConfirmed => "Reagendamento confirmado",
            ServiceAppointmentStatus.CancelledByClient => "Cancelado pelo cliente",
            ServiceAppointmentStatus.CancelledByProvider => "Cancelado pelo prestador",
            ServiceAppointmentStatus.Completed => "Concluido",
            ServiceAppointmentStatus.Arrived => "Prestador no local",
            ServiceAppointmentStatus.InProgress => "Servico em andamento",
            _ => "Atualizacao de agendamento"
        };
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private readonly record struct ProposalComparisonReference(
        decimal? LowestPrice,
        int? FastestLeadTimeHours,
        int? HighestWarrantyDays,
        int MaxReviewCount);
}
