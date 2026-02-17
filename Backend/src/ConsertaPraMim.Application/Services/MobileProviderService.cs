using System.Globalization;
using System.Text;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class MobileProviderService : IMobileProviderService
{
    private readonly IServiceRequestService _serviceRequestService;
    private readonly IProposalService _proposalService;
    private readonly IServiceAppointmentService _serviceAppointmentService;
    private readonly IChatService _chatService;
    private readonly IProfileService _profileService;
    private readonly IUserPresenceTracker _userPresenceTracker;
    private readonly IUserRepository _userRepository;
    private readonly IServiceCategoryRepository _serviceCategoryRepository;

    public MobileProviderService(
        IServiceRequestService serviceRequestService,
        IProposalService proposalService,
        IServiceAppointmentService serviceAppointmentService,
        IChatService chatService,
        IProfileService profileService,
        IUserPresenceTracker userPresenceTracker,
        IUserRepository userRepository,
        IServiceCategoryRepository serviceCategoryRepository)
    {
        _serviceRequestService = serviceRequestService;
        _proposalService = proposalService;
        _serviceAppointmentService = serviceAppointmentService;
        _chatService = chatService;
        _profileService = profileService;
        _userPresenceTracker = userPresenceTracker;
        _userRepository = userRepository;
        _serviceCategoryRepository = serviceCategoryRepository;
    }

    public async Task<MobileProviderDashboardResponseDto> GetDashboardAsync(
        Guid providerUserId,
        int takeNearbyRequests = 20,
        int takeAgenda = 10)
    {
        var normalizedTakeNearby = Math.Clamp(takeNearbyRequests, 1, 100);
        var normalizedTakeAgenda = Math.Clamp(takeAgenda, 1, 50);
        var provider = await _userRepository.GetByIdAsync(providerUserId);
        var providerName = string.IsNullOrWhiteSpace(provider?.Name) ? "Prestador" : provider!.Name;

        var requests = (await _serviceRequestService.GetAllAsync(providerUserId, UserRole.Provider.ToString()))
            .OrderBy(r => r.DistanceKm ?? double.MaxValue)
            .ThenByDescending(r => r.CreatedAt)
            .ToList();
        var proposals = (await _proposalService.GetByProviderAsync(providerUserId)).ToList();
        var proposalRequestIds = proposals
            .Select(p => p.RequestId)
            .ToHashSet();
        var categories = await _serviceCategoryRepository.GetAllAsync();
        var categoriesByNormalizedName = BuildCategoryIconMap(categories);

        var nearbyCards = requests
            .Take(normalizedTakeNearby)
            .Select(request => MapRequestCard(request, categoriesByNormalizedName, proposalRequestIds.Contains(request.Id)))
            .ToList();

        var appointments = await _serviceAppointmentService.GetMyAppointmentsAsync(providerUserId, UserRole.Provider.ToString());
        var nowUtc = DateTime.UtcNow;
        var pendingAppointmentsCount = appointments.Count(appointment =>
            string.Equals(appointment.Status, "PendingProviderConfirmation", StringComparison.OrdinalIgnoreCase));
        var upcomingConfirmedVisitsCount = appointments.Count(appointment =>
            (string.Equals(appointment.Status, "Confirmed", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(appointment.Status, "RescheduleConfirmed", StringComparison.OrdinalIgnoreCase)) &&
            appointment.WindowStartUtc >= nowUtc);

        var highlightAppointments = appointments
            .Where(appointment =>
                IsPendingProviderActionStatus(appointment.Status) ||
                IsConfirmedOperationalStatus(appointment.Status))
            .OrderBy(appointment => IsPendingProviderActionStatus(appointment.Status) ? 0 : 1)
            .ThenBy(appointment => appointment.WindowStartUtc)
            .Take(normalizedTakeAgenda)
            .ToList();

        var requestDetailsCache = new Dictionary<Guid, ServiceRequestDto>();
        foreach (var appointment in highlightAppointments)
        {
            if (requestDetailsCache.ContainsKey(appointment.ServiceRequestId))
            {
                continue;
            }

            var requestDetails = await _serviceRequestService.GetByIdAsync(
                appointment.ServiceRequestId,
                providerUserId,
                UserRole.Provider.ToString());
            if (requestDetails != null)
            {
                requestDetailsCache[appointment.ServiceRequestId] = requestDetails;
            }
        }

        var agendaHighlights = highlightAppointments
            .Select(appointment =>
            {
                requestDetailsCache.TryGetValue(appointment.ServiceRequestId, out var request);
                return new MobileProviderAppointmentHighlightDto(
                    appointment.Id,
                    appointment.ServiceRequestId,
                    appointment.Status,
                    ResolveAppointmentStatusLabel(appointment.Status),
                    appointment.WindowStartUtc,
                    appointment.WindowEndUtc,
                    request?.Category,
                    request?.ClientName);
            })
            .ToList();

        return new MobileProviderDashboardResponseDto(
            providerName,
            new MobileProviderDashboardKpiDto(
                requests.Count,
                proposals.Count(proposal => !proposal.Accepted),
                proposals.Count(proposal => proposal.Accepted),
                pendingAppointmentsCount,
                upcomingConfirmedVisitsCount),
            nearbyCards,
            agendaHighlights);
    }

    public async Task<MobileProviderRequestsResponseDto> GetNearbyRequestsAsync(
        Guid providerUserId,
        string? searchTerm = null,
        int take = 50)
    {
        var normalizedTake = Math.Clamp(take, 1, 200);
        var requests = (await _serviceRequestService.GetAllAsync(providerUserId, UserRole.Provider.ToString(), searchTerm))
            .OrderBy(request => request.DistanceKm ?? double.MaxValue)
            .ThenByDescending(request => request.CreatedAt)
            .ToList();
        var proposals = (await _proposalService.GetByProviderAsync(providerUserId)).ToList();
        var proposalRequestIds = proposals
            .Select(proposal => proposal.RequestId)
            .ToHashSet();
        var categories = await _serviceCategoryRepository.GetAllAsync();
        var categoriesByNormalizedName = BuildCategoryIconMap(categories);

        var cards = requests
            .Take(normalizedTake)
            .Select(request => MapRequestCard(request, categoriesByNormalizedName, proposalRequestIds.Contains(request.Id)))
            .ToList();

        return new MobileProviderRequestsResponseDto(cards, requests.Count);
    }

    public async Task<MobileProviderRequestDetailsResponseDto?> GetRequestDetailsAsync(Guid providerUserId, Guid requestId)
    {
        var request = await _serviceRequestService.GetByIdAsync(requestId, providerUserId, UserRole.Provider.ToString());
        if (request == null)
        {
            return null;
        }

        var proposals = (await _proposalService.GetByProviderAsync(providerUserId))
            .Where(proposal => proposal.RequestId == requestId)
            .OrderByDescending(proposal => proposal.CreatedAt)
            .ToList();
        var existingProposal = proposals.FirstOrDefault();
        var categories = await _serviceCategoryRepository.GetAllAsync();
        var categoriesByNormalizedName = BuildCategoryIconMap(categories);

        return new MobileProviderRequestDetailsResponseDto(
            MapRequestCard(request, categoriesByNormalizedName, existingProposal != null),
            existingProposal == null ? null : MapProposalSummary(existingProposal),
            existingProposal == null && IsRequestEligibleForProposal(request.Status));
    }

    public async Task<MobileProviderProposalsResponseDto> GetMyProposalsAsync(Guid providerUserId, int take = 100)
    {
        var normalizedTake = Math.Clamp(take, 1, 200);
        var proposals = (await _proposalService.GetByProviderAsync(providerUserId))
            .OrderByDescending(proposal => proposal.CreatedAt)
            .ToList();

        var items = proposals
            .Take(normalizedTake)
            .Select(MapProposalSummary)
            .ToList();

        return new MobileProviderProposalsResponseDto(
            items,
            proposals.Count,
            proposals.Count(proposal => proposal.Accepted),
            proposals.Count(proposal => !proposal.Accepted));
    }

    public async Task<MobileProviderProposalOperationResultDto> CreateProposalAsync(
        Guid providerUserId,
        Guid requestId,
        MobileProviderCreateProposalRequestDto request)
    {
        var serviceRequest = await _serviceRequestService.GetByIdAsync(
            requestId,
            providerUserId,
            UserRole.Provider.ToString());

        if (serviceRequest == null)
        {
            return new MobileProviderProposalOperationResultDto(
                false,
                ErrorCode: "mobile_provider_request_not_found",
                ErrorMessage: "Pedido nao encontrado para o prestador autenticado.");
        }

        if (!IsRequestEligibleForProposal(serviceRequest.Status))
        {
            return new MobileProviderProposalOperationResultDto(
                false,
                ErrorCode: "mobile_provider_request_not_eligible_for_proposal",
                ErrorMessage: "Este pedido nao permite envio de nova proposta no momento.");
        }

        var existingProposal = (await _proposalService.GetByProviderAsync(providerUserId))
            .FirstOrDefault(proposal => proposal.RequestId == requestId);
        if (existingProposal != null)
        {
            return new MobileProviderProposalOperationResultDto(
                false,
                ErrorCode: "mobile_provider_proposal_already_exists",
                ErrorMessage: "Voce ja enviou proposta para este pedido.");
        }

        if (request.EstimatedValue.HasValue && request.EstimatedValue.Value < 0)
        {
            return new MobileProviderProposalOperationResultDto(
                false,
                ErrorCode: "mobile_provider_proposal_invalid_estimated_value",
                ErrorMessage: "O valor estimado nao pode ser negativo.");
        }

        var normalizedMessage = NormalizeText(request.Message);
        var proposalId = await _proposalService.CreateAsync(
            providerUserId,
            new CreateProposalDto(requestId, request.EstimatedValue, normalizedMessage));

        var createdProposal = (await _proposalService.GetByProviderAsync(providerUserId))
            .OrderByDescending(proposal => proposal.CreatedAt)
            .FirstOrDefault(proposal => proposal.Id == proposalId);

        var proposalSummary = createdProposal == null
            ? new MobileProviderProposalSummaryDto(
                proposalId,
                requestId,
                request.EstimatedValue,
                normalizedMessage,
                false,
                false,
                "Aguardando cliente",
                DateTime.UtcNow)
            : MapProposalSummary(createdProposal);

        return new MobileProviderProposalOperationResultDto(
            true,
            new MobileProviderCreateProposalResponseDto(
                proposalSummary,
                "Proposta enviada com sucesso."));
    }

    public async Task<MobileProviderAgendaResponseDto> GetAgendaAsync(
        Guid providerUserId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? statusFilter = null,
        int take = 50)
    {
        var normalizedTake = Math.Clamp(take, 1, 200);
        var appointments = (await _serviceAppointmentService.GetMyAppointmentsAsync(
                providerUserId,
                UserRole.Provider.ToString(),
                fromUtc,
                toUtc))
            .OrderBy(appointment => appointment.WindowStartUtc)
            .ToList();

        var filteredAppointments = appointments
            .Where(appointment => MatchesStatusFilter(appointment.Status, statusFilter))
            .ToList();

        var requestCache = new Dictionary<Guid, ServiceRequestDto>();
        foreach (var appointment in filteredAppointments)
        {
            if (requestCache.ContainsKey(appointment.ServiceRequestId))
            {
                continue;
            }

            var request = await _serviceRequestService.GetByIdAsync(
                appointment.ServiceRequestId,
                providerUserId,
                UserRole.Provider.ToString());
            if (request != null)
            {
                requestCache[appointment.ServiceRequestId] = request;
            }
        }

        var pendingSource = filteredAppointments
            .Where(appointment => IsPendingProviderActionStatus(appointment.Status))
            .OrderBy(appointment => appointment.ExpiresAtUtc ?? appointment.WindowStartUtc)
            .ThenBy(appointment => appointment.WindowStartUtc)
            .ToList();
        var upcomingSource = filteredAppointments
            .Where(appointment => IsUpcomingAgendaStatus(appointment.Status))
            .OrderBy(appointment => appointment.WindowStartUtc)
            .ToList();

        var pendingItems = pendingSource
            .Take(normalizedTake)
            .Select(appointment =>
            {
                requestCache.TryGetValue(appointment.ServiceRequestId, out var request);
                return MapAgendaItem(appointment, request);
            })
            .ToList();
        var upcomingItems = upcomingSource
            .Take(normalizedTake)
            .Select(appointment =>
            {
                requestCache.TryGetValue(appointment.ServiceRequestId, out var request);
                return MapAgendaItem(appointment, request);
            })
            .ToList();

        return new MobileProviderAgendaResponseDto(
            pendingItems,
            upcomingItems,
            pendingSource.Count,
            upcomingSource.Count);
    }

    public async Task<MobileProviderAgendaOperationResultDto> ConfirmAgendaAppointmentAsync(
        Guid providerUserId,
        Guid appointmentId)
    {
        var result = await _serviceAppointmentService.ConfirmAsync(
            providerUserId,
            UserRole.Provider.ToString(),
            appointmentId);
        if (!result.Success || result.Appointment == null)
        {
            return new MobileProviderAgendaOperationResultDto(
                false,
                ErrorCode: result.ErrorCode,
                ErrorMessage: result.ErrorMessage);
        }

        var request = await _serviceRequestService.GetByIdAsync(
            result.Appointment.ServiceRequestId,
            providerUserId,
            UserRole.Provider.ToString());

        return new MobileProviderAgendaOperationResultDto(
            true,
            MapAgendaItem(result.Appointment, request),
            "Agendamento confirmado com sucesso.");
    }

    public async Task<MobileProviderAgendaOperationResultDto> RejectAgendaAppointmentAsync(
        Guid providerUserId,
        Guid appointmentId,
        MobileProviderRejectAgendaRequestDto request)
    {
        var reason = NormalizeText(request.Reason);
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new MobileProviderAgendaOperationResultDto(
                false,
                ErrorCode: "mobile_provider_agenda_reject_reason_required",
                ErrorMessage: "Informe o motivo da recusa.");
        }

        var result = await _serviceAppointmentService.RejectAsync(
            providerUserId,
            UserRole.Provider.ToString(),
            appointmentId,
            new RejectServiceAppointmentRequestDto(reason));
        if (!result.Success || result.Appointment == null)
        {
            return new MobileProviderAgendaOperationResultDto(
                false,
                ErrorCode: result.ErrorCode,
                ErrorMessage: result.ErrorMessage);
        }

        var serviceRequest = await _serviceRequestService.GetByIdAsync(
            result.Appointment.ServiceRequestId,
            providerUserId,
            UserRole.Provider.ToString());

        return new MobileProviderAgendaOperationResultDto(
            true,
            MapAgendaItem(result.Appointment, serviceRequest),
            "Agendamento recusado.");
    }

    public async Task<MobileProviderAgendaOperationResultDto> RespondAgendaRescheduleAsync(
        Guid providerUserId,
        Guid appointmentId,
        MobileProviderRespondRescheduleRequestDto request)
    {
        var reason = NormalizeText(request.Reason);
        var result = await _serviceAppointmentService.RespondRescheduleAsync(
            providerUserId,
            UserRole.Provider.ToString(),
            appointmentId,
            new RespondServiceAppointmentRescheduleRequestDto(request.Accept, reason));
        if (!result.Success || result.Appointment == null)
        {
            return new MobileProviderAgendaOperationResultDto(
                false,
                ErrorCode: result.ErrorCode,
                ErrorMessage: result.ErrorMessage);
        }

        var serviceRequest = await _serviceRequestService.GetByIdAsync(
            result.Appointment.ServiceRequestId,
            providerUserId,
            UserRole.Provider.ToString());

        return new MobileProviderAgendaOperationResultDto(
            true,
            MapAgendaItem(result.Appointment, serviceRequest),
            request.Accept ? "Reagendamento confirmado com sucesso." : "Reagendamento recusado.");
    }

    public async Task<MobileProviderChatConversationsResponseDto> GetChatConversationsAsync(Guid providerUserId)
    {
        var summaries = await _chatService.GetActiveConversationsAsync(providerUserId, UserRole.Provider.ToString());
        if (summaries.Count == 0)
        {
            return new MobileProviderChatConversationsResponseDto(Array.Empty<MobileProviderChatConversationSummaryDto>(), 0, 0);
        }

        var items = new List<MobileProviderChatConversationSummaryDto>(summaries.Count);
        foreach (var summary in summaries.OrderByDescending(summary => summary.LastMessageAt))
        {
            string? providerStatus = null;
            if (string.Equals(summary.CounterpartRole, "Provider", StringComparison.OrdinalIgnoreCase))
            {
                providerStatus = (await _profileService.GetProviderOperationalStatusAsync(summary.CounterpartUserId))?.ToString();
            }

            items.Add(new MobileProviderChatConversationSummaryDto(
                summary.RequestId,
                summary.ProviderId,
                summary.CounterpartUserId,
                summary.CounterpartRole,
                summary.CounterpartName,
                summary.Title,
                summary.LastMessagePreview,
                summary.LastMessageAt,
                summary.UnreadMessages,
                _userPresenceTracker.IsOnline(summary.CounterpartUserId),
                providerStatus));
        }

        return new MobileProviderChatConversationsResponseDto(
            items,
            items.Count,
            items.Sum(item => item.UnreadMessages));
    }

    public async Task<MobileProviderChatMessagesResponseDto> GetChatMessagesAsync(Guid providerUserId, Guid requestId)
    {
        var messages = await _chatService.GetConversationHistoryAsync(
            requestId,
            providerUserId,
            providerUserId,
            UserRole.Provider.ToString());

        var mapped = messages
            .Select(MapChatMessage)
            .ToList();

        return new MobileProviderChatMessagesResponseDto(
            requestId,
            providerUserId,
            mapped,
            mapped.Count);
    }

    public async Task<MobileProviderSendChatMessageResponseDto> SendChatMessageAsync(
        Guid providerUserId,
        Guid requestId,
        MobileProviderSendChatMessageRequestDto request)
    {
        var attachments = request.Attachments?
            .Select(attachment => new ChatAttachmentInputDto(
                attachment.FileUrl,
                attachment.FileName,
                attachment.ContentType,
                attachment.SizeBytes))
            .ToList();

        var sent = await _chatService.SendMessageAsync(
            requestId,
            providerUserId,
            providerUserId,
            UserRole.Provider.ToString(),
            request.Text,
            attachments);

        if (sent == null)
        {
            return new MobileProviderSendChatMessageResponseDto(
                false,
                ErrorCode: "mobile_provider_chat_send_failed",
                ErrorMessage: "Nao foi possivel enviar a mensagem para esta conversa.");
        }

        return new MobileProviderSendChatMessageResponseDto(
            true,
            Message: MapChatMessage(sent));
    }

    public async Task<MobileProviderChatReceiptOperationResponseDto> MarkChatConversationDeliveredAsync(
        Guid providerUserId,
        Guid requestId)
    {
        var receipts = await _chatService.MarkConversationDeliveredAsync(
            requestId,
            providerUserId,
            providerUserId,
            UserRole.Provider.ToString());

        return new MobileProviderChatReceiptOperationResponseDto(
            true,
            receipts.Select(MapChatReceipt).ToList());
    }

    public async Task<MobileProviderChatReceiptOperationResponseDto> MarkChatConversationReadAsync(
        Guid providerUserId,
        Guid requestId)
    {
        var receipts = await _chatService.MarkConversationReadAsync(
            requestId,
            providerUserId,
            providerUserId,
            UserRole.Provider.ToString());

        return new MobileProviderChatReceiptOperationResponseDto(
            true,
            receipts.Select(MapChatReceipt).ToList());
    }

    private static MobileProviderRequestCardDto MapRequestCard(
        ServiceRequestDto request,
        IReadOnlyDictionary<string, string> categoryIcons,
        bool alreadyProposed)
    {
        return new MobileProviderRequestCardDto(
            request.Id,
            request.Category,
            ResolveCategoryIcon(request.Category, categoryIcons),
            request.Description,
            request.Status,
            request.CreatedAt,
            request.Street,
            request.City,
            request.Zip,
            request.DistanceKm,
            request.EstimatedValue,
            alreadyProposed);
    }

    private static MobileProviderProposalSummaryDto MapProposalSummary(ProposalDto proposal)
    {
        var statusLabel = proposal.Accepted ? "Aceita" : "Aguardando cliente";

        return new MobileProviderProposalSummaryDto(
            proposal.Id,
            proposal.RequestId,
            proposal.EstimatedValue,
            proposal.Message,
            proposal.Accepted,
            false,
            statusLabel,
            proposal.CreatedAt);
    }

    private static MobileProviderAgendaItemDto MapAgendaItem(ServiceAppointmentDto appointment, ServiceRequestDto? request)
    {
        return new MobileProviderAgendaItemDto(
            appointment.Id,
            appointment.ServiceRequestId,
            appointment.Status,
            ResolveAppointmentStatusLabel(appointment.Status),
            appointment.WindowStartUtc,
            appointment.WindowEndUtc,
            request?.Category,
            request?.Description,
            request?.ClientName,
            request?.Street,
            request?.City,
            request?.Zip,
            string.Equals(appointment.Status, "PendingProviderConfirmation", StringComparison.OrdinalIgnoreCase),
            string.Equals(appointment.Status, "PendingProviderConfirmation", StringComparison.OrdinalIgnoreCase),
            string.Equals(appointment.Status, "RescheduleRequestedByClient", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsRequestEligibleForProposal(string status)
    {
        return string.Equals(status, ServiceRequestStatus.Created.ToString(), StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, ServiceRequestStatus.Matching.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPendingProviderActionStatus(string status)
    {
        return string.Equals(status, "PendingProviderConfirmation", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "RescheduleRequestedByClient", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConfirmedOperationalStatus(string status)
    {
        return string.Equals(status, "Confirmed", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "RescheduleConfirmed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUpcomingAgendaStatus(string status)
    {
        return string.Equals(status, "Confirmed", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "RescheduleConfirmed", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "Arrived", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "InProgress", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesStatusFilter(string status, string? statusFilter)
    {
        if (string.IsNullOrWhiteSpace(statusFilter) || string.Equals(statusFilter, "all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(statusFilter, "pending", StringComparison.OrdinalIgnoreCase))
        {
            return IsPendingProviderActionStatus(status);
        }

        if (string.Equals(statusFilter, "upcoming", StringComparison.OrdinalIgnoreCase))
        {
            return IsUpcomingAgendaStatus(status);
        }

        return string.Equals(status, statusFilter, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveAppointmentStatusLabel(string status)
    {
        return status switch
        {
            "PendingProviderConfirmation" => "Aguardando sua confirmacao",
            "RescheduleRequestedByClient" => "Cliente pediu reagendamento",
            "Confirmed" => "Confirmado",
            "RescheduleConfirmed" => "Reagendamento confirmado",
            "Arrived" => "Chegada registrada",
            "InProgress" => "Em atendimento",
            "Completed" => "Concluido",
            "CancelledByClient" => "Cancelado pelo cliente",
            "CancelledByProvider" => "Cancelado por voce",
            _ => status
        };
    }

    private static IReadOnlyDictionary<string, string> BuildCategoryIconMap(IReadOnlyList<ServiceCategoryDefinition> categories)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var category in categories)
        {
            var normalizedName = NormalizeSearchValue(category.Name);
            if (!string.IsNullOrWhiteSpace(normalizedName) && !map.ContainsKey(normalizedName))
            {
                map[normalizedName] = string.IsNullOrWhiteSpace(category.Icon) ? "build_circle" : category.Icon;
            }

            var normalizedSlug = NormalizeSearchValue(category.Slug);
            if (!string.IsNullOrWhiteSpace(normalizedSlug) && !map.ContainsKey(normalizedSlug))
            {
                map[normalizedSlug] = string.IsNullOrWhiteSpace(category.Icon) ? "build_circle" : category.Icon;
            }

            var normalizedLegacy = NormalizeSearchValue(category.LegacyCategory.ToString());
            if (!string.IsNullOrWhiteSpace(normalizedLegacy) && !map.ContainsKey(normalizedLegacy))
            {
                map[normalizedLegacy] = string.IsNullOrWhiteSpace(category.Icon) ? "build_circle" : category.Icon;
            }
        }

        return map;
    }

    private static string ResolveCategoryIcon(string categoryName, IReadOnlyDictionary<string, string> categoryIcons)
    {
        var normalized = NormalizeSearchValue(categoryName);
        if (!string.IsNullOrWhiteSpace(normalized) && categoryIcons.TryGetValue(normalized, out var icon))
        {
            return icon;
        }

        return normalized switch
        {
            var value when value.Contains("eletric") => "bolt",
            var value when value.Contains("hidraul") || value.Contains("plumb") => "water_drop",
            var value when value.Contains("alven") || value.Contains("mason") => "construction",
            var value when value.Contains("limpez") || value.Contains("clean") => "cleaning_services",
            var value when value.Contains("eletrodomest") || value.Contains("appliance") => "kitchen",
            var value when value.Contains("eletron") => "memory",
            _ => "build_circle"
        };
    }

    private static string? NormalizeText(string? text)
    {
        var normalized = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string NormalizeSearchValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static MobileProviderChatMessageDto MapChatMessage(ChatMessageDto message)
    {
        return new MobileProviderChatMessageDto(
            message.Id,
            message.RequestId,
            message.ProviderId,
            message.SenderId,
            message.SenderName,
            message.SenderRole,
            message.Text,
            message.CreatedAt,
            message.Attachments.Select(attachment => new MobileProviderChatAttachmentDto(
                attachment.Id,
                attachment.FileUrl,
                attachment.FileName,
                attachment.ContentType,
                attachment.SizeBytes,
                attachment.MediaKind)).ToList(),
            message.DeliveredAt,
            message.ReadAt);
    }

    private static MobileProviderChatMessageReceiptDto MapChatReceipt(ChatMessageReceiptDto receipt)
    {
        return new MobileProviderChatMessageReceiptDto(
            receipt.MessageId,
            receipt.RequestId,
            receipt.ProviderId,
            receipt.DeliveredAt,
            receipt.ReadAt);
    }
}
