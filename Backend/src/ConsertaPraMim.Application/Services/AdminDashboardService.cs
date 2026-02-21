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
    private readonly IAppointmentReminderDispatchRepository? _appointmentReminderDispatchRepository;
    private readonly IProviderCreditRepository? _providerCreditRepository;

    public AdminDashboardService(
        IUserRepository userRepository,
        IServiceRequestRepository requestRepository,
        IProposalRepository proposalRepository,
        IChatMessageRepository chatMessageRepository,
        IUserPresenceTracker userPresenceTracker,
        IPlanGovernanceService planGovernanceService,
        IAppointmentReminderDispatchRepository? appointmentReminderDispatchRepository = null,
        IProviderCreditRepository? providerCreditRepository = null)
    {
        _userRepository = userRepository;
        _requestRepository = requestRepository;
        _proposalRepository = proposalRepository;
        _chatMessageRepository = chatMessageRepository;
        _userPresenceTracker = userPresenceTracker;
        _planGovernanceService = planGovernanceService;
        _appointmentReminderDispatchRepository = appointmentReminderDispatchRepository;
        _providerCreditRepository = providerCreditRepository;
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

        var providersByOperationalStatus = users
            .Where(u => u.Role == UserRole.Provider && u.ProviderProfile is not null)
            .Select(u => u.ProviderProfile!.OperationalStatus)
            .GroupBy(status => status)
            .Select(g => new AdminStatusCountDto(
                FormatProviderOperationalStatus(g.Key),
                g.Count()))
            .OrderBy(x => GetProviderOperationalStatusOrder(x.Status))
            .ThenByDescending(x => x.Count)
            .ToList();

        var requestsByCategory = requestsInPeriod
            .GroupBy(ResolveCategoryName)
            .Select(g => new AdminCategoryCountDto(g.Key, g.Count()))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Category)
            .ToList();

        var reviews = requests
            .SelectMany(r => r.Reviews ?? Enumerable.Empty<Review>())
            .ToList();

        var userNameById = users
            .GroupBy(u => u.Id)
            .ToDictionary(g => g.Key, g => g.First().Name);

        var reviewRanking = BuildReviewRanking(reviews, userNameById);
        var providerReviewRanking = reviewRanking
            .Where(item => item.UserRole == "Prestador")
            .OrderByDescending(item => item.AverageRating)
            .ThenByDescending(item => item.TotalReviews)
            .ThenBy(item => item.UserName)
            .Take(10)
            .ToList();

        var clientReviewRanking = reviewRanking
            .Where(item => item.UserRole == "Cliente")
            .OrderByDescending(item => item.AverageRating)
            .ThenByDescending(item => item.TotalReviews)
            .ThenBy(item => item.UserName)
            .Take(10)
            .ToList();

        var reviewOutliers = BuildReviewOutliers(reviewRanking)
            .OrderByDescending(item => item.OneStarRatePercent)
            .ThenBy(item => item.AverageRating)
            .ThenByDescending(item => item.TotalReviews)
            .ThenBy(item => item.UserName)
            .Take(15)
            .ToList();

        var providerNameById = users
            .Where(u => u.Role == UserRole.Provider)
            .GroupBy(u => u.Id)
            .ToDictionary(g => g.Key, g => g.First().Name);

        var failedTransactionsInPeriod = filteredRequests
            .SelectMany(r => r.PaymentTransactions)
            .Where(t => t.Status == PaymentTransactionStatus.Failed)
            .Where(t =>
            {
                var occurredAt = ResolvePaymentFailureTimestamp(t);
                return occurredAt >= fromUtc && occurredAt <= toUtc;
            })
            .ToList();

        var paymentFailuresByProvider = failedTransactionsInPeriod
            .GroupBy(t => t.ProviderId)
            .Select(g => new AdminPaymentFailureByProviderDto(
                ProviderId: g.Key,
                ProviderName: providerNameById.TryGetValue(g.Key, out var providerName) && !string.IsNullOrWhiteSpace(providerName)
                    ? providerName
                    : "Prestador",
                FailedTransactions: g.Count(),
                AffectedRequests: g.Select(x => x.ServiceRequestId).Distinct().Count(),
                LastFailureAtUtc: g.Max(ResolvePaymentFailureTimestamp)))
            .OrderByDescending(x => x.FailedTransactions)
            .ThenByDescending(x => x.LastFailureAtUtc)
            .ThenBy(x => x.ProviderName)
            .Take(10)
            .ToList();

        var paymentFailuresByChannel = failedTransactionsInPeriod
            .GroupBy(t => t.Method)
            .Select(g => new AdminStatusCountDto(FormatPaymentMethod(g.Key), g.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Status)
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
        var providerCreditKpis = await BuildProviderCreditKpisAsync(users, fromUtc, toUtc, nowUtc);
        var agendaOperationalKpis = BuildAgendaOperationalKpis(filteredRequests, fromUtc, toUtc);
        var reminderDispatchKpis = await BuildReminderDispatchKpisAsync(fromUtc, toUtc);

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
            AppointmentsByOperationalStatus: appointmentsByOperationalStatus,
            PaymentFailuresByProvider: paymentFailuresByProvider,
            PaymentFailuresByChannel: paymentFailuresByChannel,
            ProviderReviewRanking: providerReviewRanking,
            ClientReviewRanking: clientReviewRanking,
            ReviewOutliers: reviewOutliers,
            CreditsGrantedInPeriod: providerCreditKpis.CreditsGrantedInPeriod,
            CreditsConsumedInPeriod: providerCreditKpis.CreditsConsumedInPeriod,
            CreditsOpenBalance: providerCreditKpis.CreditsOpenBalance,
            CreditsExpiringInNext30Days: providerCreditKpis.CreditsExpiringInNext30Days,
            AppointmentConfirmationInSlaRatePercent: agendaOperationalKpis.ConfirmationInSlaRatePercent,
            AppointmentRescheduleRatePercent: agendaOperationalKpis.RescheduleRatePercent,
            AppointmentCancellationRatePercent: agendaOperationalKpis.CancellationRatePercent,
            ReminderFailureRatePercent: reminderDispatchKpis.FailureRatePercent,
            ReminderAttemptsInPeriod: reminderDispatchKpis.AttemptsInPeriod,
            ReminderFailuresInPeriod: reminderDispatchKpis.FailuresInPeriod,
            ProvidersByOperationalStatus: providersByOperationalStatus);
    }

    public async Task<AdminCoverageMapDto> GetCoverageMapAsync()
    {
        // Repositories in this request share the same scoped DbContext.
        // Keep database calls sequential to avoid concurrent operations on the same context instance.
        var users = (await _userRepository.GetAllAsync()).ToList();
        var requests = (await _requestRepository.GetAllAsync()).ToList();

        var providers = users
            .Where(u => u.Role == UserRole.Provider && u.ProviderProfile is not null)
            .Select(u => new { User = u, Profile = u.ProviderProfile! })
            .Where(x => x.Profile.BaseLatitude.HasValue && x.Profile.BaseLongitude.HasValue)
            .Where(x => IsValidCoordinate(x.Profile.BaseLatitude!.Value, x.Profile.BaseLongitude!.Value))
            .Select(x => new AdminCoverageMapProviderDto(
                ProviderId: x.User.Id,
                ProviderName: string.IsNullOrWhiteSpace(x.User.Name) ? "Prestador" : x.User.Name.Trim(),
                Latitude: x.Profile.BaseLatitude!.Value,
                Longitude: x.Profile.BaseLongitude!.Value,
                RadiusKm: x.Profile.RadiusKm > 0 ? x.Profile.RadiusKm : 1d,
                OperationalStatus: x.Profile.OperationalStatus.ToString(),
                IsActive: x.User.IsActive))
            .OrderBy(x => x.ProviderName)
            .ToList();

        var mappedRequests = requests
            .Where(r => IsValidCoordinate(r.Latitude, r.Longitude))
            .Select(r => new AdminCoverageMapRequestDto(
                RequestId: r.Id,
                Status: r.Status.ToString(),
                Category: ResolveCategoryName(r),
                Description: string.IsNullOrWhiteSpace(r.Description) ? "Sem descricao" : r.Description,
                AddressCity: r.AddressCity,
                AddressStreet: r.AddressStreet,
                Latitude: r.Latitude,
                Longitude: r.Longitude,
                CreatedAtUtc: r.CreatedAt))
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToList();

        return new AdminCoverageMapDto(
            Providers: providers,
            Requests: mappedRequests,
            GeneratedAtUtc: DateTime.UtcNow);
    }

    private async Task<ProviderCreditDashboardKpi> BuildProviderCreditKpisAsync(
        IReadOnlyCollection<User> users,
        DateTime fromUtc,
        DateTime toUtc,
        DateTime nowUtc)
    {
        if (_providerCreditRepository == null)
        {
            return ProviderCreditDashboardKpi.Empty;
        }

        var providerIds = users
            .Where(u => u.Role == UserRole.Provider)
            .Select(u => u.Id)
            .Distinct()
            .ToList();

        if (providerIds.Count == 0)
        {
            return ProviderCreditDashboardKpi.Empty;
        }

        decimal grantedInPeriod = 0m;
        decimal consumedInPeriod = 0m;
        decimal openBalance = 0m;
        decimal expiringInNext30Days = 0m;
        var expiringThresholdUtc = nowUtc.AddDays(30);

        foreach (var providerId in providerIds)
        {
            var wallet = await _providerCreditRepository.GetWalletAsync(providerId);
            if (wallet == null)
            {
                continue;
            }

            openBalance += Math.Max(0m, wallet.CurrentBalance);

            var entries = await _providerCreditRepository.GetEntriesChronologicalAsync(providerId);
            if (entries.Count == 0)
            {
                continue;
            }

            grantedInPeriod += entries
                .Where(e => e.EntryType == ProviderCreditLedgerEntryType.Grant)
                .Where(e => e.EffectiveAtUtc >= fromUtc && e.EffectiveAtUtc <= toUtc)
                .Sum(e => e.Amount);

            consumedInPeriod += entries
                .Where(e => e.EntryType == ProviderCreditLedgerEntryType.Debit)
                .Where(e => e.EffectiveAtUtc >= fromUtc && e.EffectiveAtUtc <= toUtc)
                .Sum(e => e.Amount);

            expiringInNext30Days += CalculateExpiringCreditsAmount(entries, wallet.CurrentBalance, nowUtc, expiringThresholdUtc);
        }

        return new ProviderCreditDashboardKpi(
            CreditsGrantedInPeriod: RoundCurrency(grantedInPeriod),
            CreditsConsumedInPeriod: RoundCurrency(consumedInPeriod),
            CreditsOpenBalance: RoundCurrency(openBalance),
            CreditsExpiringInNext30Days: RoundCurrency(expiringInNext30Days));
    }

    private async Task<ReminderDispatchKpi> BuildReminderDispatchKpisAsync(DateTime fromUtc, DateTime toUtc)
    {
        if (_appointmentReminderDispatchRepository == null)
        {
            return ReminderDispatchKpi.Empty;
        }

        var sentCount = await _appointmentReminderDispatchRepository.CountAsync(
            status: AppointmentReminderDispatchStatus.Sent,
            fromUtc: fromUtc,
            toUtc: toUtc);

        var failedRetryableCount = await _appointmentReminderDispatchRepository.CountAsync(
            status: AppointmentReminderDispatchStatus.FailedRetryable,
            fromUtc: fromUtc,
            toUtc: toUtc);

        var failedPermanentCount = await _appointmentReminderDispatchRepository.CountAsync(
            status: AppointmentReminderDispatchStatus.FailedPermanent,
            fromUtc: fromUtc,
            toUtc: toUtc);

        var attempts = sentCount + failedRetryableCount + failedPermanentCount;
        var failures = failedRetryableCount + failedPermanentCount;

        return new ReminderDispatchKpi(
            FailureRatePercent: CalculateRatePercent(failures, attempts),
            AttemptsInPeriod: attempts,
            FailuresInPeriod: failures);
    }

    private static AgendaOperationalKpi BuildAgendaOperationalKpis(
        IReadOnlyCollection<ServiceRequest> requests,
        DateTime fromUtc,
        DateTime toUtc)
    {
        var appointmentsInPeriod = requests
            .SelectMany(r => r.Appointments ?? Enumerable.Empty<ServiceAppointment>())
            .Where(a => a.CreatedAt >= fromUtc && a.CreatedAt <= toUtc)
            .ToList();

        if (appointmentsInPeriod.Count == 0)
        {
            return AgendaOperationalKpi.Empty;
        }

        var confirmationCandidates = appointmentsInPeriod
            .Where(a => a.ExpiresAtUtc.HasValue)
            .ToList();

        var confirmedWithinSla = confirmationCandidates.Count(a =>
            a.ConfirmedAtUtc.HasValue &&
            a.ConfirmedAtUtc.Value <= a.ExpiresAtUtc!.Value);

        var rescheduledCount = appointmentsInPeriod.Count(a =>
            a.RescheduleRequestedAtUtc.HasValue ||
            a.Status == ServiceAppointmentStatus.RescheduleRequestedByClient ||
            a.Status == ServiceAppointmentStatus.RescheduleRequestedByProvider ||
            a.Status == ServiceAppointmentStatus.RescheduleConfirmed);

        var cancelledCount = appointmentsInPeriod.Count(a =>
            a.Status == ServiceAppointmentStatus.CancelledByClient ||
            a.Status == ServiceAppointmentStatus.CancelledByProvider);

        return new AgendaOperationalKpi(
            ConfirmationInSlaRatePercent: CalculateRatePercent(confirmedWithinSla, confirmationCandidates.Count),
            RescheduleRatePercent: CalculateRatePercent(rescheduledCount, appointmentsInPeriod.Count),
            CancellationRatePercent: CalculateRatePercent(cancelledCount, appointmentsInPeriod.Count));
    }

    private static decimal CalculateExpiringCreditsAmount(
        IReadOnlyCollection<ProviderCreditLedgerEntry> entries,
        decimal walletBalance,
        DateTime nowUtc,
        DateTime expiringThresholdUtc)
    {
        if (entries.Count == 0 || walletBalance <= 0m)
        {
            return 0m;
        }

        var lots = new List<CreditLot>();
        foreach (var entry in entries
                     .OrderBy(e => e.EffectiveAtUtc)
                     .ThenBy(e => e.CreatedAt))
        {
            switch (entry.EntryType)
            {
                case ProviderCreditLedgerEntryType.Grant:
                case ProviderCreditLedgerEntryType.Reversal:
                    if (entry.Amount > 0m)
                    {
                        lots.Add(new CreditLot(entry.EffectiveAtUtc, entry.ExpiresAtUtc, entry.Amount));
                    }

                    break;

                case ProviderCreditLedgerEntryType.Debit:
                case ProviderCreditLedgerEntryType.Expire:
                    ConsumeLots(lots, entry.Amount);
                    break;
            }
        }

        var expiringAmount = lots
            .Where(l => l.RemainingAmount > 0m)
            .Where(l => l.ExpiresAtUtc.HasValue)
            .Where(l => l.ExpiresAtUtc!.Value > nowUtc && l.ExpiresAtUtc.Value <= expiringThresholdUtc)
            .Sum(l => l.RemainingAmount);

        return RoundCurrency(Math.Min(Math.Max(0m, walletBalance), expiringAmount));
    }

    private static void ConsumeLots(List<CreditLot> lots, decimal amount)
    {
        var remaining = RoundCurrency(Math.Max(0m, amount));
        if (remaining <= 0m)
        {
            return;
        }

        foreach (var lot in lots
                     .Where(l => l.RemainingAmount > 0m)
                     .OrderBy(l => l.EffectiveAtUtc))
        {
            if (remaining <= 0m)
            {
                break;
            }

            var consumed = RoundCurrency(Math.Min(lot.RemainingAmount, remaining));
            lot.RemainingAmount -= consumed;
            remaining -= consumed;
        }
    }

    private static decimal RoundCurrency(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal CalculateRatePercent(int numerator, int denominator)
    {
        if (denominator <= 0 || numerator <= 0)
        {
            return 0m;
        }

        return decimal.Round((numerator * 100m) / denominator, 1, MidpointRounding.AwayFromZero);
    }

    private static bool IsValidCoordinate(double latitude, double longitude)
    {
        if (double.IsNaN(latitude) || double.IsInfinity(latitude) ||
            double.IsNaN(longitude) || double.IsInfinity(longitude))
        {
            return false;
        }

        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return false;
        }

        return Math.Abs(latitude) > 0.000001d || Math.Abs(longitude) > 0.000001d;
    }

    private sealed class CreditLot
    {
        public CreditLot(DateTime effectiveAtUtc, DateTime? expiresAtUtc, decimal amount)
        {
            EffectiveAtUtc = effectiveAtUtc;
            ExpiresAtUtc = expiresAtUtc;
            RemainingAmount = RoundCurrency(Math.Max(0m, amount));
        }

        public DateTime EffectiveAtUtc { get; }
        public DateTime? ExpiresAtUtc { get; }
        public decimal RemainingAmount { get; set; }
    }

    private sealed record ProviderCreditDashboardKpi(
        decimal CreditsGrantedInPeriod,
        decimal CreditsConsumedInPeriod,
        decimal CreditsOpenBalance,
        decimal CreditsExpiringInNext30Days)
    {
        public static ProviderCreditDashboardKpi Empty { get; } = new(0m, 0m, 0m, 0m);
    }

    private sealed record AgendaOperationalKpi(
        decimal ConfirmationInSlaRatePercent,
        decimal RescheduleRatePercent,
        decimal CancellationRatePercent)
    {
        public static AgendaOperationalKpi Empty { get; } = new(0m, 0m, 0m);
    }

    private sealed record ReminderDispatchKpi(
        decimal FailureRatePercent,
        int AttemptsInPeriod,
        int FailuresInPeriod)
    {
        public static ReminderDispatchKpi Empty { get; } = new(0m, 0, 0);
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

    private static IReadOnlyList<AdminReviewRankingItemDto> BuildReviewRanking(
        IReadOnlyCollection<Review> reviews,
        IReadOnlyDictionary<Guid, string> userNameById)
    {
        return reviews
            .GroupBy(r => new { r.RevieweeUserId, r.RevieweeRole })
            .Select(group =>
            {
                var totalReviews = group.Count();
                var fiveStarCount = group.Count(r => r.Rating == 5);
                var oneStarCount = group.Count(r => r.Rating == 1);
                var averageRating = Math.Round(group.Average(r => r.Rating), 2, MidpointRounding.AwayFromZero);
                var roleLabel = group.Key.RevieweeRole == UserRole.Provider ? "Prestador" : "Cliente";
                var userName = userNameById.TryGetValue(group.Key.RevieweeUserId, out var mappedName) &&
                               !string.IsNullOrWhiteSpace(mappedName)
                    ? mappedName
                    : group.Key.RevieweeRole == UserRole.Provider
                        ? "Prestador"
                        : "Cliente";

                return new AdminReviewRankingItemDto(
                    UserId: group.Key.RevieweeUserId,
                    UserName: userName,
                    UserRole: roleLabel,
                    AverageRating: averageRating,
                    TotalReviews: totalReviews,
                    FiveStarCount: fiveStarCount,
                    OneStarCount: oneStarCount,
                    LastReviewAtUtc: group.Max(r => r.CreatedAt));
            })
            .Where(item => item.TotalReviews > 0)
            .ToList();
    }

    private static IReadOnlyList<AdminReviewOutlierDto> BuildReviewOutliers(
        IReadOnlyCollection<AdminReviewRankingItemDto> ranking)
    {
        var outliers = new List<AdminReviewOutlierDto>();

        foreach (var item in ranking)
        {
            if (item.TotalReviews < 3)
            {
                continue;
            }

            var oneStarRatePercent = item.TotalReviews == 0
                ? 0m
                : decimal.Round((decimal)item.OneStarCount * 100m / item.TotalReviews, 1, MidpointRounding.AwayFromZero);

            string? reason = null;
            if (item.AverageRating <= 2.5 && item.TotalReviews >= 5)
            {
                reason = "Media baixa recorrente";
            }
            else if (oneStarRatePercent >= 60m)
            {
                reason = "Alta concentracao de 1 estrela";
            }
            else if (item.AverageRating >= 4.9 && item.TotalReviews >= 15)
            {
                reason = "Reputacao excepcional (validar consistencia)";
            }

            if (reason == null)
            {
                continue;
            }

            outliers.Add(new AdminReviewOutlierDto(
                UserId: item.UserId,
                UserName: item.UserName,
                UserRole: item.UserRole,
                AverageRating: item.AverageRating,
                TotalReviews: item.TotalReviews,
                OneStarRatePercent: oneStarRatePercent,
                Reason: reason));
        }

        return outliers;
    }

    private static DateTime ResolvePaymentFailureTimestamp(ServicePaymentTransaction transaction)
    {
        return transaction.ProcessedAtUtc
               ?? transaction.UpdatedAt
               ?? transaction.CreatedAt;
    }

    private static string FormatPaymentMethod(PaymentTransactionMethod method)
    {
        return method switch
        {
            PaymentTransactionMethod.Pix => "PIX",
            PaymentTransactionMethod.Card => "Cartao",
            _ => method.ToString()
        };
    }

    private static string FormatProviderOperationalStatus(ProviderOperationalStatus status)
    {
        return status switch
        {
            ProviderOperationalStatus.Ausente => "Offline",
            ProviderOperationalStatus.EmAtendimento => "Em atendimento",
            ProviderOperationalStatus.Online => "Online",
            _ => status.ToString()
        };
    }

    private static int GetProviderOperationalStatusOrder(string status)
    {
        return status switch
        {
            "Offline" => 0,
            "Em atendimento" => 1,
            "Online" => 2,
            _ => 99
        };
    }
}
