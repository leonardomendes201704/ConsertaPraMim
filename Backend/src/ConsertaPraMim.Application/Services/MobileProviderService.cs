using System.Globalization;
using System.Text;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace ConsertaPraMim.Application.Services;

public class MobileProviderService : IMobileProviderService
{
    private const int DefaultMapPinPageSize = 120;
    private const int MinMapPinPageSize = 20;
    private const int MaxMapPinPageSize = 200;
    private const int MaxMapPinTake = 500;

    private readonly IServiceRequestService _serviceRequestService;
    private readonly IProposalService _proposalService;
    private readonly IServiceAppointmentService _serviceAppointmentService;
    private readonly IServiceAppointmentChecklistService _serviceAppointmentChecklistService;
    private readonly IChatService _chatService;
    private readonly IProfileService _profileService;
    private readonly IUserPresenceTracker _userPresenceTracker;
    private readonly IUserRepository _userRepository;
    private readonly IServiceCategoryRepository _serviceCategoryRepository;
    private readonly ISupportTicketRepository _supportTicketRepository;
    private readonly INotificationService? _notificationService;
    private readonly ILogger<MobileProviderService>? _logger;

    public MobileProviderService(
        IServiceRequestService serviceRequestService,
        IProposalService proposalService,
        IServiceAppointmentService serviceAppointmentService,
        IServiceAppointmentChecklistService serviceAppointmentChecklistService,
        IChatService chatService,
        IProfileService profileService,
        IUserPresenceTracker userPresenceTracker,
        IUserRepository userRepository,
        IServiceCategoryRepository serviceCategoryRepository,
        ISupportTicketRepository supportTicketRepository,
        INotificationService? notificationService = null,
        ILogger<MobileProviderService>? logger = null)
    {
        _serviceRequestService = serviceRequestService;
        _proposalService = proposalService;
        _serviceAppointmentService = serviceAppointmentService;
        _serviceAppointmentChecklistService = serviceAppointmentChecklistService;
        _chatService = chatService;
        _profileService = profileService;
        _userPresenceTracker = userPresenceTracker;
        _userRepository = userRepository;
        _serviceCategoryRepository = serviceCategoryRepository;
        _supportTicketRepository = supportTicketRepository;
        _notificationService = notificationService;
        _logger = logger;
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

    public async Task<MobileProviderProfileSettingsDto?> GetProfileSettingsAsync(Guid providerUserId)
    {
        var profile = await _profileService.GetProfileAsync(providerUserId);
        var providerProfile = profile?.ProviderProfile;
        if (profile == null || providerProfile == null)
        {
            return null;
        }

        var allowedCategories = (providerProfile.PlanAllowedCategories?.Any() == true
            ? providerProfile.PlanAllowedCategories
            : Enum.GetValues<ServiceCategory>().ToList())
            .Distinct()
            .OrderBy(category => (int)category)
            .ToList();
        var selectedCategories = (providerProfile.Categories ?? new List<ServiceCategory>())
            .Distinct()
            .ToHashSet();
        var maxRadius = Math.Max(1.0, providerProfile.PlanMaxRadiusKm ?? 50.0);
        var maxAllowedCategories = providerProfile.PlanMaxAllowedCategories ?? allowedCategories.Count;

        var statusOptions = Enum.GetValues<ProviderOperationalStatus>()
            .Select(status => new MobileProviderProfileStatusOptionDto(
                (int)status,
                status.ToString(),
                ResolveProviderOperationalStatusLabel(status),
                status == providerProfile.OperationalStatus))
            .ToList();

        var categoryOptions = allowedCategories
            .Select(category => new MobileProviderProfileCategoryOptionDto(
                (int)category,
                category.ToString(),
                category.ToPtBr(),
                ResolveProviderCategoryIcon(category),
                selectedCategories.Contains(category)))
            .ToList();

        return new MobileProviderProfileSettingsDto(
            profile.Name,
            profile.Email,
            profile.Phone,
            profile.Role,
            profile.ProfilePictureUrl,
            providerProfile.Plan.ToString(),
            providerProfile.OnboardingStatus.ToString(),
            providerProfile.IsOnboardingCompleted,
            providerProfile.Rating,
            providerProfile.ReviewCount,
            providerProfile.HasOperationalCompliancePending,
            providerProfile.OperationalComplianceNotes,
            providerProfile.RadiusKm,
            providerProfile.BaseZipCode,
            providerProfile.BaseLatitude,
            providerProfile.BaseLongitude,
            maxRadius,
            maxAllowedCategories,
            statusOptions,
            categoryOptions);
    }

    public async Task<MobileProviderProfileSettingsOperationResultDto> UpdateProfileSettingsAsync(
        Guid providerUserId,
        MobileProviderUpdateProfileSettingsRequestDto request)
    {
        if (!Enum.IsDefined(typeof(ProviderOperationalStatus), request.OperationalStatus))
        {
            return new MobileProviderProfileSettingsOperationResultDto(
                false,
                ErrorCode: "mobile_provider_profile_invalid_operational_status",
                ErrorMessage: "Status operacional invalido.");
        }

        var profile = await _profileService.GetProfileAsync(providerUserId);
        var providerProfile = profile?.ProviderProfile;
        if (providerProfile == null)
        {
            return new MobileProviderProfileSettingsOperationResultDto(
                false,
                ErrorCode: "mobile_provider_profile_not_found",
                ErrorMessage: "Perfil de prestador nao encontrado.");
        }

        var allowedCategories = (providerProfile.PlanAllowedCategories?.Any() == true
            ? providerProfile.PlanAllowedCategories
            : Enum.GetValues<ServiceCategory>().ToList())
            .Distinct()
            .OrderBy(category => (int)category)
            .ToList();
        var allowedCategorySet = allowedCategories.ToHashSet();
        var maxAllowedCategories = providerProfile.PlanMaxAllowedCategories ?? allowedCategories.Count;
        var maxRadius = Math.Max(1.0, providerProfile.PlanMaxRadiusKm ?? 50.0);

        if (request.RadiusKm < 1)
        {
            return new MobileProviderProfileSettingsOperationResultDto(
                false,
                ErrorCode: "mobile_provider_profile_invalid_radius",
                ErrorMessage: "Raio de atendimento deve ser maior ou igual a 1 km.");
        }

        if (request.RadiusKm > maxRadius)
        {
            return new MobileProviderProfileSettingsOperationResultDto(
                false,
                ErrorCode: "mobile_provider_profile_radius_exceeds_plan_limit",
                ErrorMessage: $"Seu plano permite no maximo {maxRadius:0.#} km.");
        }

        var selectedCategories = (request.Categories ?? Array.Empty<int>())
            .Where(value => Enum.IsDefined(typeof(ServiceCategory), value))
            .Select(value => (ServiceCategory)value)
            .Distinct()
            .ToList();
        if (!selectedCategories.Any())
        {
            return new MobileProviderProfileSettingsOperationResultDto(
                false,
                ErrorCode: "mobile_provider_profile_categories_required",
                ErrorMessage: "Selecione pelo menos uma especialidade.");
        }

        if (selectedCategories.Any(category => !allowedCategorySet.Contains(category)))
        {
            return new MobileProviderProfileSettingsOperationResultDto(
                false,
                ErrorCode: "mobile_provider_profile_category_not_allowed_by_plan",
                ErrorMessage: "Uma ou mais categorias nao sao permitidas no plano atual.");
        }

        if (selectedCategories.Count > maxAllowedCategories)
        {
            return new MobileProviderProfileSettingsOperationResultDto(
                false,
                ErrorCode: "mobile_provider_profile_categories_exceed_plan_limit",
                ErrorMessage: $"Seu plano permite no maximo {maxAllowedCategories} categoria(s).");
        }

        var normalizedZip = NormalizeZipCode(request.BaseZipCode);
        var requestHasLatitude = request.BaseLatitude.HasValue;
        var requestHasLongitude = request.BaseLongitude.HasValue;
        if (requestHasLatitude != requestHasLongitude)
        {
            return new MobileProviderProfileSettingsOperationResultDto(
                false,
                ErrorCode: "mobile_provider_profile_invalid_location",
                ErrorMessage: "Latitude e longitude devem ser informadas juntas.");
        }

        var zipChanged = !string.IsNullOrWhiteSpace(normalizedZip) &&
            !string.Equals(normalizedZip, providerProfile.BaseZipCode, StringComparison.Ordinal);
        if (zipChanged && !(requestHasLatitude && requestHasLongitude))
        {
            return new MobileProviderProfileSettingsOperationResultDto(
                false,
                ErrorCode: "mobile_provider_profile_zip_requires_coordinates",
                ErrorMessage: "Use a busca de CEP para obter latitude e longitude antes de salvar.");
        }

        var status = (ProviderOperationalStatus)request.OperationalStatus;
        var update = new UpdateProviderProfileDto(
            request.RadiusKm,
            normalizedZip ?? providerProfile.BaseZipCode,
            requestHasLatitude ? request.BaseLatitude : providerProfile.BaseLatitude,
            requestHasLongitude ? request.BaseLongitude : providerProfile.BaseLongitude,
            selectedCategories,
            status);

        var success = await _profileService.UpdateProviderProfileAsync(providerUserId, update);
        if (!success)
        {
            return new MobileProviderProfileSettingsOperationResultDto(
                false,
                ErrorCode: "mobile_provider_profile_update_rejected",
                ErrorMessage: "Nao foi possivel salvar o perfil com os dados informados.");
        }

        var refreshed = await GetProfileSettingsAsync(providerUserId);
        return new MobileProviderProfileSettingsOperationResultDto(
            true,
            refreshed,
            "Perfil atualizado com sucesso.");
    }

    public async Task<MobileProviderProfileSettingsOperationResultDto> UpdateProfileOperationalStatusAsync(
        Guid providerUserId,
        MobileProviderUpdateProfileOperationalStatusRequestDto request)
    {
        if (!Enum.IsDefined(typeof(ProviderOperationalStatus), request.OperationalStatus))
        {
            return new MobileProviderProfileSettingsOperationResultDto(
                false,
                ErrorCode: "mobile_provider_profile_invalid_operational_status",
                ErrorMessage: "Status operacional invalido.");
        }

        var status = (ProviderOperationalStatus)request.OperationalStatus;
        var success = await _profileService.UpdateProviderOperationalStatusAsync(providerUserId, status);
        if (!success)
        {
            return new MobileProviderProfileSettingsOperationResultDto(
                false,
                ErrorCode: "mobile_provider_profile_operational_status_update_failed",
                ErrorMessage: "Nao foi possivel atualizar o status operacional.");
        }

        var refreshed = await GetProfileSettingsAsync(providerUserId);
        return new MobileProviderProfileSettingsOperationResultDto(
            true,
            refreshed,
            $"Status operacional atualizado para {ResolveProviderOperationalStatusLabel(status)}.");
    }

    public async Task<MobileProviderCoverageMapDto> GetCoverageMapAsync(
        Guid providerUserId,
        string? categoryFilter = null,
        double? maxDistanceKm = null,
        int pinPage = 1,
        int pinPageSize = DefaultMapPinPageSize)
    {
        var profile = await _profileService.GetProfileAsync(providerUserId);
        var providerProfile = profile?.ProviderProfile;
        var normalizedPinPage = Math.Max(1, pinPage);
        var normalizedPinPageSize = Math.Clamp(pinPageSize, MinMapPinPageSize, MaxMapPinPageSize);

        if (providerProfile?.BaseLatitude is not double providerLat || providerProfile.BaseLongitude is not double providerLng)
        {
            return new MobileProviderCoverageMapDto(
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                normalizedPinPage,
                normalizedPinPageSize,
                0,
                false,
                Array.Empty<MobileProviderCoverageMapPinDto>());
        }

        var interestRadiusKm = providerProfile.RadiusKm > 0 ? providerProfile.RadiusKm : 5.0;
        var defaultMapSearchRadiusKm = Math.Clamp(interestRadiusKm * 4, 40.0, 250.0);
        var requestedDistanceKm = maxDistanceKm.HasValue && maxDistanceKm.Value > 0
            ? Math.Min(maxDistanceKm.Value, defaultMapSearchRadiusKm)
            : defaultMapSearchRadiusKm;

        var normalizedCategoryFilter = NormalizeSearchValue(categoryFilter);
        if (string.IsNullOrWhiteSpace(normalizedCategoryFilter))
        {
            normalizedCategoryFilter = string.Empty;
        }

        var takeForLookup = Math.Clamp(normalizedPinPage * normalizedPinPageSize, normalizedPinPageSize, MaxMapPinTake);
        var mapPins = await _serviceRequestService.GetMapPinsForProviderAsync(providerUserId, requestedDistanceKm, takeForLookup);
        if (!string.IsNullOrWhiteSpace(normalizedCategoryFilter))
        {
            mapPins = mapPins.Where(pin =>
                string.Equals(
                    NormalizeSearchValue(pin.Category),
                    normalizedCategoryFilter,
                    StringComparison.Ordinal));
        }

        var categories = await _serviceCategoryRepository.GetAllAsync();
        var categoryIcons = BuildCategoryIconMap(categories);
        var filteredPins = mapPins.ToList();
        var filteredTotal = filteredPins.Count;
        var skip = (normalizedPinPage - 1) * normalizedPinPageSize;
        var pagedPins = skip >= filteredTotal
            ? new List<ProviderServiceMapPinDto>()
            : filteredPins.Skip(skip).Take(normalizedPinPageSize).ToList();
        var hasMorePins = skip + pagedPins.Count < filteredTotal;

        return new MobileProviderCoverageMapDto(
            true,
            providerLat,
            providerLng,
            interestRadiusKm,
            requestedDistanceKm,
            providerProfile.BaseZipCode,
            string.IsNullOrWhiteSpace(normalizedCategoryFilter) ? null : normalizedCategoryFilter,
            maxDistanceKm,
            normalizedPinPage,
            normalizedPinPageSize,
            filteredTotal,
            hasMorePins,
            pagedPins.Select(pin => new MobileProviderCoverageMapPinDto(
                    pin.RequestId,
                    pin.Category,
                    ResolveCategoryIcon(pin.Category, categoryIcons),
                    pin.Description,
                    pin.Street,
                    pin.City,
                    pin.Zip,
                    pin.CreatedAt,
                    pin.Latitude,
                    pin.Longitude,
                    Math.Round(pin.DistanceKm, 2),
                    pin.IsWithinInterestRadius,
                    pin.IsCategoryMatch))
                .ToList());
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

    public async Task<MobileProviderSupportTicketOperationResultDto> CreateSupportTicketAsync(
        Guid providerUserId,
        MobileProviderCreateSupportTicketRequestDto request)
    {
        var subject = NormalizeText(request.Subject);
        var category = NormalizeText(request.Category) ?? "General";
        var initialMessage = NormalizeText(request.InitialMessage);

        if (string.IsNullOrWhiteSpace(subject))
        {
            return new MobileProviderSupportTicketOperationResultDto(
                false,
                ErrorCode: "mobile_provider_support_subject_required",
                ErrorMessage: "Assunto do chamado e obrigatorio.");
        }

        if (subject.Length > 220)
        {
            return new MobileProviderSupportTicketOperationResultDto(
                false,
                ErrorCode: "mobile_provider_support_subject_too_long",
                ErrorMessage: "Assunto deve ter no maximo 220 caracteres.");
        }

        if (category.Length > 80)
        {
            return new MobileProviderSupportTicketOperationResultDto(
                false,
                ErrorCode: "mobile_provider_support_category_too_long",
                ErrorMessage: "Categoria deve ter no maximo 80 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(initialMessage))
        {
            return new MobileProviderSupportTicketOperationResultDto(
                false,
                ErrorCode: "mobile_provider_support_message_required",
                ErrorMessage: "Mensagem inicial do chamado e obrigatoria.");
        }

        if (initialMessage.Length > 3000)
        {
            return new MobileProviderSupportTicketOperationResultDto(
                false,
                ErrorCode: "mobile_provider_support_message_too_long",
                ErrorMessage: "Mensagem deve ter no maximo 3000 caracteres.");
        }

        if (!TryParseSupportTicketPriority(request.Priority, out var priority))
        {
            return new MobileProviderSupportTicketOperationResultDto(
                false,
                ErrorCode: "mobile_provider_support_invalid_priority",
                ErrorMessage: "Prioridade invalida.");
        }

        var now = DateTime.UtcNow;
        var ticket = new SupportTicket
        {
            ProviderId = providerUserId,
            Subject = subject,
            Category = category,
            Priority = priority,
            Status = SupportTicketStatus.Open,
            OpenedAtUtc = now,
            LastInteractionAtUtc = now
        };

        ticket.AddMessage(
            authorUserId: providerUserId,
            authorRole: UserRole.Provider,
            messageText: initialMessage,
            isInternal: false,
            messageType: "ProviderOpened");

        await _supportTicketRepository.AddAsync(ticket);
        var persisted = await _supportTicketRepository.GetProviderTicketByIdWithMessagesAsync(providerUserId, ticket.Id) ?? ticket;

        await TryNotifyAdminsAsync(
            ticket,
            $"Novo chamado de suporte #{BuildTicketShortCode(ticket.Id)}",
            $"Prestador abriu chamado: {subject}.",
            $"/AdminSupportTickets/Details/{ticket.Id}",
            "provider_support_ticket_created");

        return new MobileProviderSupportTicketOperationResultDto(
            true,
            Ticket: MapSupportTicketDetails(persisted));
    }

    public async Task<MobileProviderSupportTicketListResponseDto> GetSupportTicketsAsync(
        Guid providerUserId,
        MobileProviderSupportTicketListQueryDto query)
    {
        var safeQuery = query ?? new MobileProviderSupportTicketListQueryDto();
        var safePage = safeQuery.Page < 1 ? 1 : safeQuery.Page;
        var safePageSize = safeQuery.PageSize <= 0 ? 20 : Math.Min(safeQuery.PageSize, 100);

        var hasStatus = TryParseSupportTicketStatus(safeQuery.Status, out var status);
        var hasPriority = TryParseSupportTicketPriority(safeQuery.Priority, out var priority);

        var (items, totalCount) = await _supportTicketRepository.GetProviderTicketsAsync(
            providerUserId,
            hasStatus ? status : null,
            hasPriority ? priority : null,
            safeQuery.Search,
            safePage,
            safePageSize);

        var mapped = items
            .Select(MapSupportTicketSummary)
            .ToList();

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)safePageSize);

        return new MobileProviderSupportTicketListResponseDto(
            mapped,
            safePage,
            safePageSize,
            totalCount,
            totalPages);
    }

    public async Task<MobileProviderSupportTicketOperationResultDto> GetSupportTicketDetailsAsync(
        Guid providerUserId,
        Guid ticketId)
    {
        if (ticketId == Guid.Empty)
        {
            return new MobileProviderSupportTicketOperationResultDto(
                false,
                ErrorCode: "mobile_provider_support_invalid_ticket",
                ErrorMessage: "Chamado invalido.");
        }

        var ticket = await _supportTicketRepository.GetProviderTicketByIdWithMessagesAsync(providerUserId, ticketId);
        if (ticket == null)
        {
            return new MobileProviderSupportTicketOperationResultDto(
                false,
                ErrorCode: "mobile_provider_support_ticket_not_found",
                ErrorMessage: "Chamado nao encontrado.");
        }

        return new MobileProviderSupportTicketOperationResultDto(
            true,
            Ticket: MapSupportTicketDetails(ticket));
    }

    public async Task<MobileProviderSupportTicketOperationResultDto> AddSupportTicketMessageAsync(
        Guid providerUserId,
        Guid ticketId,
        MobileProviderSupportTicketMessageRequestDto request)
    {
        if (ticketId == Guid.Empty)
        {
            return new MobileProviderSupportTicketOperationResultDto(
                false,
                ErrorCode: "mobile_provider_support_invalid_ticket",
                ErrorMessage: "Chamado invalido.");
        }

        var messageText = NormalizeText(request.Message);
        if (string.IsNullOrWhiteSpace(messageText))
        {
            return new MobileProviderSupportTicketOperationResultDto(
                false,
                ErrorCode: "mobile_provider_support_message_required",
                ErrorMessage: "Mensagem do chamado e obrigatoria.");
        }

        if (messageText.Length > 3000)
        {
            return new MobileProviderSupportTicketOperationResultDto(
                false,
                ErrorCode: "mobile_provider_support_message_too_long",
                ErrorMessage: "Mensagem deve ter no maximo 3000 caracteres.");
        }

        var ticket = await _supportTicketRepository.GetProviderTicketByIdWithMessagesAsync(providerUserId, ticketId);
        if (ticket == null)
        {
            return new MobileProviderSupportTicketOperationResultDto(
                false,
                ErrorCode: "mobile_provider_support_ticket_not_found",
                ErrorMessage: "Chamado nao encontrado.");
        }

        if (!CanProviderReply(ticket.Status))
        {
            return new MobileProviderSupportTicketOperationResultDto(
                false,
                ErrorCode: "mobile_provider_support_invalid_state",
                ErrorMessage: "Chamado nao permite novas mensagens neste status.");
        }

        var createdMessage = ticket.AddMessage(
            authorUserId: providerUserId,
            authorRole: UserRole.Provider,
            messageText: messageText,
            isInternal: false,
            messageType: "ProviderReply");

        if (ticket.Status == SupportTicketStatus.WaitingProvider)
        {
            ticket.ChangeStatus(SupportTicketStatus.InProgress);
        }

        await _supportTicketRepository.UpdateAsync(ticket);

        await TryNotifyAdminsAsync(
            ticket,
            $"Nova mensagem do prestador no chamado #{BuildTicketShortCode(ticket.Id)}",
            TruncateForPreview(messageText, 240) ?? "Nova mensagem recebida.",
            $"/AdminSupportTickets/Details/{ticket.Id}",
            "provider_support_message_added",
            ticket.AssignedAdminUserId);

        return new MobileProviderSupportTicketOperationResultDto(
            true,
            Ticket: MapSupportTicketDetails(ticket),
            Message: MapSupportTicketMessage(createdMessage));
    }

    public async Task<MobileProviderSupportTicketOperationResultDto> CloseSupportTicketAsync(
        Guid providerUserId,
        Guid ticketId)
    {
        if (ticketId == Guid.Empty)
        {
            return new MobileProviderSupportTicketOperationResultDto(
                false,
                ErrorCode: "mobile_provider_support_invalid_ticket",
                ErrorMessage: "Chamado invalido.");
        }

        var ticket = await _supportTicketRepository.GetProviderTicketByIdWithMessagesAsync(providerUserId, ticketId);
        if (ticket == null)
        {
            return new MobileProviderSupportTicketOperationResultDto(
                false,
                ErrorCode: "mobile_provider_support_ticket_not_found",
                ErrorMessage: "Chamado nao encontrado.");
        }

        if (ticket.Status == SupportTicketStatus.Closed)
        {
            return new MobileProviderSupportTicketOperationResultDto(
                false,
                ErrorCode: "mobile_provider_support_invalid_state",
                ErrorMessage: "Chamado ja esta fechado.");
        }

        ticket.ChangeStatus(SupportTicketStatus.Closed);
        ticket.AddMessage(
            authorUserId: providerUserId,
            authorRole: UserRole.Provider,
            messageText: "Chamado encerrado pelo prestador.",
            isInternal: false,
            messageType: "ProviderClosed");

        await _supportTicketRepository.UpdateAsync(ticket);

        await TryNotifyAdminsAsync(
            ticket,
            $"Chamado #{BuildTicketShortCode(ticket.Id)} encerrado pelo prestador",
            "O prestador encerrou o chamado de suporte.",
            $"/AdminSupportTickets/Details/{ticket.Id}",
            "provider_support_ticket_closed",
            ticket.AssignedAdminUserId);

        return new MobileProviderSupportTicketOperationResultDto(
            true,
            Ticket: MapSupportTicketDetails(ticket));
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

    public async Task<MobileProviderAgendaOperationResultDto> MarkAgendaArrivalAsync(
        Guid providerUserId,
        Guid appointmentId,
        MobileProviderMarkArrivalRequestDto request)
    {
        var result = await _serviceAppointmentService.MarkArrivedAsync(
            providerUserId,
            UserRole.Provider.ToString(),
            appointmentId,
            new MarkServiceAppointmentArrivalRequestDto(
                request.Latitude,
                request.Longitude,
                request.AccuracyMeters,
                NormalizeText(request.ManualReason)));
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
            "Chegada registrada com sucesso.");
    }

    public async Task<MobileProviderAgendaOperationResultDto> StartAgendaExecutionAsync(
        Guid providerUserId,
        Guid appointmentId,
        MobileProviderStartExecutionRequestDto request)
    {
        var result = await _serviceAppointmentService.StartExecutionAsync(
            providerUserId,
            UserRole.Provider.ToString(),
            appointmentId,
            new StartServiceAppointmentExecutionRequestDto(NormalizeText(request.Reason)));
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
            "Atendimento iniciado com sucesso.");
    }

    public async Task<MobileProviderAgendaOperationResultDto> UpdateAgendaOperationalStatusAsync(
        Guid providerUserId,
        Guid appointmentId,
        MobileProviderUpdateOperationalStatusRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.OperationalStatus))
        {
            return new MobileProviderAgendaOperationResultDto(
                false,
                ErrorCode: "invalid_request",
                ErrorMessage: "Status operacional e obrigatorio.");
        }

        var result = await _serviceAppointmentService.UpdateOperationalStatusAsync(
            providerUserId,
            UserRole.Provider.ToString(),
            appointmentId,
            new UpdateServiceAppointmentOperationalStatusRequestDto(
                request.OperationalStatus.Trim(),
                NormalizeText(request.Reason)));
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
            "Status operacional atualizado com sucesso.");
    }

    public async Task<MobileProviderChecklistResultDto> GetAppointmentChecklistAsync(
        Guid providerUserId,
        Guid appointmentId)
    {
        var result = await _serviceAppointmentChecklistService.GetChecklistAsync(
            providerUserId,
            UserRole.Provider.ToString(),
            appointmentId);
        if (!result.Success)
        {
            return new MobileProviderChecklistResultDto(
                false,
                ErrorCode: result.ErrorCode,
                ErrorMessage: result.ErrorMessage);
        }

        return new MobileProviderChecklistResultDto(
            true,
            result.Checklist);
    }

    public async Task<MobileProviderChecklistResultDto> UpdateAppointmentChecklistItemAsync(
        Guid providerUserId,
        Guid appointmentId,
        MobileProviderChecklistItemUpsertRequestDto request)
    {
        if (request.TemplateItemId == Guid.Empty)
        {
            return new MobileProviderChecklistResultDto(
                false,
                ErrorCode: "invalid_item",
                ErrorMessage: "Item de checklist invalido.");
        }

        var result = await _serviceAppointmentChecklistService.UpsertItemResponseAsync(
            providerUserId,
            UserRole.Provider.ToString(),
            appointmentId,
            new UpsertServiceChecklistItemResponseRequestDto(
                request.TemplateItemId,
                request.IsChecked,
                NormalizeText(request.Note),
                request.EvidenceUrl,
                request.EvidenceFileName,
                request.EvidenceContentType,
                request.EvidenceSizeBytes,
                request.ClearEvidence));
        if (!result.Success)
        {
            return new MobileProviderChecklistResultDto(
                false,
                ErrorCode: result.ErrorCode,
                ErrorMessage: result.ErrorMessage);
        }

        return new MobileProviderChecklistResultDto(
            true,
            result.Checklist);
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

    private static string ResolveProviderCategoryIcon(ServiceCategory category)
    {
        return category switch
        {
            ServiceCategory.Electrical => "bolt",
            ServiceCategory.Plumbing => "water_drop",
            ServiceCategory.Electronics => "memory",
            ServiceCategory.Appliances => "kitchen",
            ServiceCategory.Masonry => "construction",
            ServiceCategory.Cleaning => "cleaning_services",
            ServiceCategory.Other => "build_circle",
            _ => "build_circle"
        };
    }

    private static string ResolveProviderOperationalStatusLabel(ProviderOperationalStatus status)
    {
        return status switch
        {
            ProviderOperationalStatus.Ausente => "Ausente",
            ProviderOperationalStatus.Online => "Online",
            ProviderOperationalStatus.EmAtendimento => "Em atendimento",
            _ => status.ToString()
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

    private static string? NormalizeZipCode(string? zipCode)
    {
        if (string.IsNullOrWhiteSpace(zipCode))
        {
            return null;
        }

        var digits = new string(zipCode.Where(char.IsDigit).ToArray());
        return digits.Length == 8 ? digits : null;
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

    private static MobileProviderSupportTicketSummaryDto MapSupportTicketSummary(SupportTicket ticket)
    {
        var visibleMessages = GetVisibleSupportMessages(ticket);
        var lastVisibleMessage = visibleMessages.LastOrDefault();

        return new MobileProviderSupportTicketSummaryDto(
            ticket.Id,
            ticket.Subject,
            ticket.Category,
            ticket.Priority.ToString(),
            ticket.Status.ToString(),
            ticket.OpenedAtUtc,
            ticket.LastInteractionAtUtc,
            ticket.ClosedAtUtc,
            ticket.AssignedAdminUserId,
            ticket.AssignedAdminUser?.Name,
            visibleMessages.Count,
            TruncateForPreview(lastVisibleMessage?.MessageText, 240));
    }

    private static MobileProviderSupportTicketDetailsDto MapSupportTicketDetails(SupportTicket ticket)
    {
        var summary = MapSupportTicketSummary(ticket);
        var messages = GetVisibleSupportMessages(ticket)
            .Select(MapSupportTicketMessage)
            .ToList();

        return new MobileProviderSupportTicketDetailsDto(
            summary,
            ticket.FirstAdminResponseAtUtc,
            messages);
    }

    private static MobileProviderSupportTicketMessageDto MapSupportTicketMessage(SupportTicketMessage message)
    {
        var authorName = message.AuthorUser?.Name;
        if (string.IsNullOrWhiteSpace(authorName))
        {
            authorName = message.AuthorRole switch
            {
                UserRole.Admin => "Admin",
                UserRole.Provider => "Prestador",
                _ => "Sistema"
            };
        }

        return new MobileProviderSupportTicketMessageDto(
            message.Id,
            message.AuthorUserId,
            message.AuthorRole.ToString(),
            authorName,
            message.MessageType,
            message.MessageText,
            message.CreatedAt);
    }

    private static IReadOnlyList<SupportTicketMessage> GetVisibleSupportMessages(SupportTicket ticket)
    {
        return (ticket.Messages ?? Array.Empty<SupportTicketMessage>())
            .Where(message => !message.IsInternal)
            .OrderBy(message => message.CreatedAt)
            .ToList();
    }

    private static bool CanProviderReply(SupportTicketStatus status)
    {
        return status is SupportTicketStatus.Open or SupportTicketStatus.InProgress or SupportTicketStatus.WaitingProvider;
    }

    private static bool TryParseSupportTicketStatus(string? raw, out SupportTicketStatus parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = raw.Trim();
        if (Enum.TryParse<SupportTicketStatus>(normalized, true, out parsed))
        {
            return true;
        }

        if (int.TryParse(normalized, out var numeric) &&
            Enum.IsDefined(typeof(SupportTicketStatus), numeric))
        {
            parsed = (SupportTicketStatus)numeric;
            return true;
        }

        return false;
    }

    private static bool TryParseSupportTicketPriority(string? raw, out SupportTicketPriority parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = raw.Trim();
        if (Enum.TryParse<SupportTicketPriority>(normalized, true, out parsed))
        {
            return true;
        }

        if (int.TryParse(normalized, out var numeric) &&
            Enum.IsDefined(typeof(SupportTicketPriority), numeric))
        {
            parsed = (SupportTicketPriority)numeric;
            return true;
        }

        return false;
    }

    private static bool TryParseSupportTicketPriority(int? raw, out SupportTicketPriority parsed)
    {
        parsed = SupportTicketPriority.Medium;
        if (!raw.HasValue)
        {
            return true;
        }

        if (!Enum.IsDefined(typeof(SupportTicketPriority), raw.Value))
        {
            return false;
        }

        parsed = (SupportTicketPriority)raw.Value;
        return true;
    }

    private static string? TruncateForPreview(string? value, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length <= maxChars)
        {
            return normalized;
        }

        return $"{normalized[..Math.Max(1, maxChars - 3)]}...";
    }

    private async Task TryNotifyAdminsAsync(
        SupportTicket ticket,
        string subject,
        string message,
        string actionUrl,
        string reason,
        Guid? preferredAdminUserId = null)
    {
        if (_notificationService == null)
        {
            return;
        }

        var recipients = await ResolveAdminRecipientsAsync(preferredAdminUserId);
        foreach (var recipientId in recipients)
        {
            try
            {
                await _notificationService.SendNotificationAsync(
                    recipientId.ToString(),
                    subject,
                    message,
                    actionUrl);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(
                    ex,
                    "Support admin notification failed. Reason={Reason} TicketId={TicketId} AdminUserId={AdminUserId}",
                    reason,
                    ticket.Id,
                    recipientId);
            }
        }
    }

    private async Task<IReadOnlyList<Guid>> ResolveAdminRecipientsAsync(Guid? preferredAdminUserId)
    {
        if (preferredAdminUserId.HasValue && preferredAdminUserId.Value != Guid.Empty)
        {
            return new[] { preferredAdminUserId.Value };
        }

        IEnumerable<User>? users;
        try
        {
            users = await _userRepository.GetAllAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to resolve admin recipients for support notification.");
            return Array.Empty<Guid>();
        }

        return (users ?? Enumerable.Empty<User>())
            .Where(user => user.Role == UserRole.Admin && user.IsActive)
            .Select(user => user.Id)
            .Distinct()
            .ToList();
    }

    private static string BuildTicketShortCode(Guid ticketId)
    {
        return ticketId.ToString("N")[..8].ToUpperInvariant();
    }
}
