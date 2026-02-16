using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class MobileClientOrderService : IMobileClientOrderService
{
    private readonly IServiceRequestRepository _serviceRequestRepository;

    public MobileClientOrderService(IServiceRequestRepository serviceRequestRepository)
    {
        _serviceRequestRepository = serviceRequestRepository;
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
                    proposal.CreatedAt));
            }
            else
            {
                events.Add(new MobileClientOrderTimelineEventDto(
                    "proposal_received",
                    "Proposta recebida",
                    $"Nova proposta enviada por {providerName}.{valueText}",
                    proposal.CreatedAt));
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
            normalizedDescription);
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
}
