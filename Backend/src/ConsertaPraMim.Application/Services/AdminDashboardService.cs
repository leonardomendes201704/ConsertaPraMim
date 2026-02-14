using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Configuration;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class AdminDashboardService : IAdminDashboardService
{
    private readonly IUserRepository _userRepository;
    private readonly IServiceRequestRepository _requestRepository;
    private readonly IProposalRepository _proposalRepository;
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly IUserPresenceTracker _userPresenceTracker;
    private readonly IPlanGovernanceService _planGovernanceService;

    public AdminDashboardService(
        IUserRepository userRepository,
        IServiceRequestRepository requestRepository,
        IProposalRepository proposalRepository,
        IChatMessageRepository chatMessageRepository,
        IUserPresenceTracker userPresenceTracker,
        IPlanGovernanceService planGovernanceService)
    {
        _userRepository = userRepository;
        _requestRepository = requestRepository;
        _proposalRepository = proposalRepository;
        _chatMessageRepository = chatMessageRepository;
        _userPresenceTracker = userPresenceTracker;
        _planGovernanceService = planGovernanceService;
    }

    public async Task<AdminDashboardDto> GetDashboardAsync(AdminDashboardQueryDto query)
    {
        var (fromUtc, toUtc) = NormalizeRange(query.FromUtc, query.ToUtc);
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);
        var normalizedEventType = NormalizeEventType(query.EventType);
        var operationalStatusFilter = NormalizeOperationalStatus(query.OperationalStatus);
        var normalizedSearchTerm = query.SearchTerm?.Trim();
        var nowUtc = DateTime.UtcNow;

        // Repositories in this request share the same scoped DbContext.
        // Keep database calls sequential to avoid concurrent operations on the same context instance.
        var users = (await _userRepository.GetAllAsync()).ToList();
        var requests = (await _requestRepository.GetAllAsync()).ToList();
        var proposals = (await _proposalRepository.GetAllAsync()).ToList();
        var chatMessagesInPeriod = (await _chatMessageRepository.GetByPeriodAsync(fromUtc, toUtc)).ToList();
        var chatMessagesLast24h = (await _chatMessageRepository.GetByPeriodAsync(nowUtc.AddHours(-24), nowUtc)).ToList();

        var filteredRequests = operationalStatusFilter.HasValue
            ? requests.Where(r => HasOperationalStatus(r, operationalStatusFilter.Value)).ToList()
            : requests;
        var filteredRequestIds = filteredRequests.Select(r => r.Id).ToHashSet();

        var requestsInPeriod = filteredRequests.Where(r => r.CreatedAt >= fromUtc && r.CreatedAt <= toUtc).ToList();
        var proposalsInPeriodQuery = proposals
            .Where(p => p.CreatedAt >= fromUtc && p.CreatedAt <= toUtc);
        if (operationalStatusFilter.HasValue)
        {
            proposalsInPeriodQuery = proposalsInPeriodQuery.Where(p => filteredRequestIds.Contains(p.RequestId));
        }

        var proposalsInPeriod = proposalsInPeriodQuery.ToList();
        var chatMessagesInPeriodFiltered = operationalStatusFilter.HasValue
            ? chatMessagesInPeriod.Where(m => filteredRequestIds.Contains(m.RequestId)).ToList()
            : chatMessagesInPeriod;
        var chatMessagesLast24hFiltered = operationalStatusFilter.HasValue
            ? chatMessagesLast24h.Where(m => filteredRequestIds.Contains(m.RequestId)).ToList()
            : chatMessagesLast24h;
        var planOffers = await _planGovernanceService.GetProviderPlanOffersAsync(nowUtc);
        var offerByPlan = planOffers.ToDictionary(x => x.Plan, x => x);

        var requestsByStatus = filteredRequests
            .GroupBy(r => r.Status)
            .OrderBy(g => g.Key)
            .Select(g => new AdminStatusCountDto(g.Key.ToString(), g.Count()))
            .ToList();

        var appointmentsByOperationalStatus = filteredRequests
            .SelectMany(r => r.Appointments)
            .Select(ResolveOperationalStatus)
            .Where(s => s.HasValue)
            .Select(s => s!.Value)
            .GroupBy(s => s)
            .Select(g => new AdminStatusCountDto(g.Key.ToPtBr(), g.Count()))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Status)
            .ToList();

        var requestsByCategory = requestsInPeriod
            .GroupBy(ResolveCategoryName)
            .Select(g => new AdminCategoryCountDto(g.Key, g.Count()))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Category)
            .ToList();

        var revenueByPlan = users
            .Where(u => u.Role == UserRole.Provider && u.IsActive && u.ProviderProfile is not null)
            .Select(u => u.ProviderProfile!)
            .Where(p => p.Plan != ProviderPlan.Trial)
            .GroupBy(p => p.Plan)
            .Select(g =>
            {
                var unitMonthlyPrice = offerByPlan.TryGetValue(g.Key, out var offer)
                    ? offer.PriceWithPromotion
                    : ProviderSubscriptionPricingCatalog.GetMonthlyPrice(g.Key);
                var providers = g.Count();
                return new AdminPlanRevenueDto(
                    Plan: g.Key.ToPtBr(),
                    Providers: providers,
                    UnitMonthlyPrice: unitMonthlyPrice,
                    TotalMonthlyRevenue: unitMonthlyPrice * providers);
            })
            .OrderByDescending(r => r.TotalMonthlyRevenue)
            .ThenBy(r => r.Plan)
            .ToList();

        var monthlySubscriptionRevenue = revenueByPlan.Sum(p => p.TotalMonthlyRevenue);
        var payingProviders = revenueByPlan.Sum(p => p.Providers);

        var activeConversationsLast24h = chatMessagesLast24hFiltered
            .Select(m => $"{m.RequestId:N}:{m.ProviderId:N}")
            .Distinct(StringComparer.Ordinal)
            .Count();

        var events = BuildEvents(requestsInPeriod, proposalsInPeriod, chatMessagesInPeriodFiltered);

        if (!string.IsNullOrWhiteSpace(normalizedEventType))
        {
            events = events
                .Where(e => e.Type.Equals(normalizedEventType, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(normalizedSearchTerm))
        {
            events = events
                .Where(e =>
                    (!string.IsNullOrWhiteSpace(e.Title) && e.Title.Contains(normalizedSearchTerm, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(e.Description) && e.Description.Contains(normalizedSearchTerm, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        var orderedEvents = events
            .OrderByDescending(e => e.CreatedAt)
            .ToList();

        var totalEvents = orderedEvents.Count;
        var pagedEvents = orderedEvents
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new AdminDashboardDto(
            TotalUsers: users.Count,
            ActiveUsers: users.Count(u => u.IsActive),
            InactiveUsers: users.Count(u => !u.IsActive),
            TotalProviders: users.Count(u => u.Role == UserRole.Provider),
            TotalClients: users.Count(u => u.Role == UserRole.Client),
            OnlineProviders: _userPresenceTracker.CountOnlineUsers(users.Where(u => u.Role == UserRole.Provider).Select(u => u.Id)),
            OnlineClients: _userPresenceTracker.CountOnlineUsers(users.Where(u => u.Role == UserRole.Client).Select(u => u.Id)),
            PayingProviders: payingProviders,
            MonthlySubscriptionRevenue: monthlySubscriptionRevenue,
            RevenueByPlan: revenueByPlan,
            TotalAdmins: users.Count(u => u.Role == UserRole.Admin),
            TotalRequests: filteredRequests.Count,
            ActiveRequests: filteredRequests.Count(r => r.Status != ServiceRequestStatus.Completed && r.Status != ServiceRequestStatus.Canceled),
            RequestsInPeriod: requestsInPeriod.Count,
            RequestsByStatus: requestsByStatus,
            RequestsByCategory: requestsByCategory,
            ProposalsInPeriod: proposalsInPeriod.Count,
            AcceptedProposalsInPeriod: proposalsInPeriod.Count(p => p.Accepted),
            ActiveChatConversationsLast24h: activeConversationsLast24h,
            FromUtc: fromUtc,
            ToUtc: toUtc,
            Page: page,
            PageSize: pageSize,
            TotalEvents: totalEvents,
            RecentEvents: pagedEvents,
            AppointmentsByOperationalStatus: appointmentsByOperationalStatus);
    }

    private static (DateTime FromUtc, DateTime ToUtc) NormalizeRange(DateTime? fromUtc, DateTime? toUtc)
    {
        var end = toUtc ?? DateTime.UtcNow;
        var start = fromUtc ?? end.AddDays(-30);

        if (start > end)
        {
            (start, end) = (end, start);
        }

        return (start, end);
    }

    private static string? NormalizeEventType(string? rawEventType)
    {
        if (string.IsNullOrWhiteSpace(rawEventType))
        {
            return null;
        }

        var value = rawEventType.Trim().ToLowerInvariant();
        return value switch
        {
            "request" => "request",
            "proposal" => "proposal",
            "chat" => "chat",
            "all" => null,
            _ => null
        };
    }

    private static ServiceAppointmentOperationalStatus? NormalizeOperationalStatus(string? rawStatus)
    {
        if (string.IsNullOrWhiteSpace(rawStatus))
        {
            return null;
        }

        return ServiceAppointmentOperationalStatusExtensions.TryParseFlexible(rawStatus, out var parsed)
            ? parsed
            : null;
    }

    private static bool HasOperationalStatus(ServiceRequest request, ServiceAppointmentOperationalStatus expectedStatus)
    {
        return request.Appointments.Any(a => ResolveOperationalStatus(a) == expectedStatus);
    }

    private static ServiceAppointmentOperationalStatus? ResolveOperationalStatus(ServiceAppointment appointment)
    {
        if (appointment.OperationalStatus.HasValue)
        {
            return appointment.OperationalStatus.Value;
        }

        if (appointment.Status == ServiceAppointmentStatus.Completed || appointment.CompletedAtUtc.HasValue)
        {
            return ServiceAppointmentOperationalStatus.Completed;
        }

        if (appointment.Status == ServiceAppointmentStatus.InProgress || appointment.StartedAtUtc.HasValue)
        {
            return ServiceAppointmentOperationalStatus.InService;
        }

        if (appointment.Status == ServiceAppointmentStatus.Arrived || appointment.ArrivedAtUtc.HasValue)
        {
            return ServiceAppointmentOperationalStatus.OnSite;
        }

        if (appointment.Status is ServiceAppointmentStatus.Confirmed or
            ServiceAppointmentStatus.RescheduleConfirmed or
            ServiceAppointmentStatus.RescheduleRequestedByClient or
            ServiceAppointmentStatus.RescheduleRequestedByProvider or
            ServiceAppointmentStatus.PendingProviderConfirmation)
        {
            return ServiceAppointmentOperationalStatus.OnTheWay;
        }

        return null;
    }

    private static List<AdminRecentEventDto> BuildEvents(
        List<ServiceRequest> requestsInPeriod,
        List<Proposal> proposalsInPeriod,
        List<ChatMessage> chatMessagesInPeriod)
    {
        var events = new List<AdminRecentEventDto>(requestsInPeriod.Count + proposalsInPeriod.Count + chatMessagesInPeriod.Count);

        events.AddRange(requestsInPeriod.Select(r => new AdminRecentEventDto(
            Type: "request",
            ReferenceId: r.Id,
            CreatedAt: r.CreatedAt,
            Title: $"Pedido criado: {r.Description}",
            Description: $"Categoria: {ResolveCategoryName(r)} | Status: {r.Status}")));

        events.AddRange(proposalsInPeriod.Select(p => new AdminRecentEventDto(
            Type: "proposal",
            ReferenceId: p.Id,
            CreatedAt: p.CreatedAt,
            Title: $"Proposta enviada para pedido {p.RequestId}",
            Description: p.Accepted ? "Proposta aceita pelo cliente." : "Proposta pendente de aceite.")));

        events.AddRange(chatMessagesInPeriod.Select(m => new AdminRecentEventDto(
            Type: "chat",
            ReferenceId: m.Id,
            CreatedAt: m.CreatedAt,
            Title: $"Mensagem de chat no pedido {m.RequestId}",
            Description: BuildChatDescription(m))));

        return events;
    }

    private static string BuildChatDescription(ChatMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            return message.Text.Length > 100 ? $"{message.Text[..100]}..." : message.Text;
        }

        var attachmentCount = message.Attachments?.Count ?? 0;
        return attachmentCount > 0
            ? $"Mensagem com {attachmentCount} anexo(s)."
            : "Mensagem sem texto.";
    }

    private static string ResolveCategoryName(ServiceRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.CategoryDefinition?.Name))
        {
            return request.CategoryDefinition.Name;
        }

        return request.Category.ToPtBr();
    }
}
