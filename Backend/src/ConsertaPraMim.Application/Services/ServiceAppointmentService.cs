using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Microsoft.Extensions.Configuration;

namespace ConsertaPraMim.Application.Services;

public class ServiceAppointmentService : IServiceAppointmentService
{
    private const int MinimumSlotDurationMinutes = 15;
    private const int MaximumSlotDurationMinutes = 240;
    private const int MaximumSlotsQueryRangeDays = 31;
    private const int MaximumAppointmentWindowMinutes = 8 * 60;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> AppointmentCreationLocks = new();
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> AppointmentOperationalLocks = new();
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> ServiceRequestScopeChangeLocks = new();
    private static readonly IReadOnlyDictionary<ProviderPlan, ScopeChangePolicy> DefaultScopeChangePolicies =
        new Dictionary<ProviderPlan, ScopeChangePolicy>
        {
            [ProviderPlan.Trial] = new ScopeChangePolicy(MaxIncrementalValue: 120m, MaxPercentOverAcceptedProposal: 30m),
            [ProviderPlan.Bronze] = new ScopeChangePolicy(MaxIncrementalValue: 500m, MaxPercentOverAcceptedProposal: 60m),
            [ProviderPlan.Silver] = new ScopeChangePolicy(MaxIncrementalValue: 1500m, MaxPercentOverAcceptedProposal: 100m),
            [ProviderPlan.Gold] = new ScopeChangePolicy(MaxIncrementalValue: 5000m, MaxPercentOverAcceptedProposal: 200m)
        };

    private static readonly IReadOnlyCollection<ServiceAppointmentStatus> BlockingStatuses = new[]
    {
        ServiceAppointmentStatus.PendingProviderConfirmation,
        ServiceAppointmentStatus.Confirmed,
        ServiceAppointmentStatus.Arrived,
        ServiceAppointmentStatus.InProgress,
        ServiceAppointmentStatus.RescheduleRequestedByClient,
        ServiceAppointmentStatus.RescheduleRequestedByProvider,
        ServiceAppointmentStatus.RescheduleConfirmed
    };

    private static readonly IReadOnlyDictionary<ServiceAppointmentOperationalStatus, IReadOnlyCollection<ServiceAppointmentOperationalStatus>> OperationalStatusTransitions =
        new Dictionary<ServiceAppointmentOperationalStatus, IReadOnlyCollection<ServiceAppointmentOperationalStatus>>
        {
            [ServiceAppointmentOperationalStatus.OnTheWay] = new[]
            {
                ServiceAppointmentOperationalStatus.OnSite
            },
            [ServiceAppointmentOperationalStatus.OnSite] = new[]
            {
                ServiceAppointmentOperationalStatus.InService
            },
            [ServiceAppointmentOperationalStatus.InService] = new[]
            {
                ServiceAppointmentOperationalStatus.WaitingParts,
                ServiceAppointmentOperationalStatus.Completed
            },
            [ServiceAppointmentOperationalStatus.WaitingParts] = new[]
            {
                ServiceAppointmentOperationalStatus.InService,
                ServiceAppointmentOperationalStatus.Completed
            },
            [ServiceAppointmentOperationalStatus.Completed] = Array.Empty<ServiceAppointmentOperationalStatus>()
        };

    private readonly IServiceAppointmentRepository _serviceAppointmentRepository;
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly IServiceCompletionTermRepository _serviceCompletionTermRepository;
    private readonly IServiceAppointmentChecklistService _serviceAppointmentChecklistService;
    private readonly IServiceScopeChangeRequestRepository _scopeChangeRequestRepository;
    private readonly IServiceRequestCommercialValueService _serviceRequestCommercialValueService;
    private readonly IServiceFinancialPolicyCalculationService? _serviceFinancialPolicyCalculationService;
    private readonly IProviderCreditService? _providerCreditService;
    private readonly IAppointmentReminderService _appointmentReminderService;
    private readonly TimeZoneInfo _availabilityTimeZone;
    private readonly int _providerConfirmationExpiryHours;
    private readonly int _cancelMinimumHoursBeforeWindow;
    private readonly int _rescheduleMinimumHoursBeforeWindow;
    private readonly int _rescheduleMaximumAdvanceDays;
    private readonly int _completionPinLength;
    private readonly int _completionPinExpiryMinutes;
    private readonly int _completionPinMaxFailedAttempts;
    private readonly int _scopeChangeClientApprovalTimeoutMinutes;
    private readonly IReadOnlyDictionary<ProviderPlan, ScopeChangePolicy> _scopeChangePolicies;

    public ServiceAppointmentService(
        IServiceAppointmentRepository serviceAppointmentRepository,
        IServiceRequestRepository serviceRequestRepository,
        IUserRepository userRepository,
        INotificationService notificationService,
        IConfiguration configuration,
        IAppointmentReminderService? appointmentReminderService = null,
        IServiceAppointmentChecklistService? serviceAppointmentChecklistService = null,
        IServiceCompletionTermRepository? serviceCompletionTermRepository = null,
        IServiceScopeChangeRequestRepository? scopeChangeRequestRepository = null,
        IServiceRequestCommercialValueService? serviceRequestCommercialValueService = null,
        IServiceFinancialPolicyCalculationService? serviceFinancialPolicyCalculationService = null,
        IProviderCreditService? providerCreditService = null)
    {
        _serviceAppointmentRepository = serviceAppointmentRepository;
        _serviceRequestRepository = serviceRequestRepository;
        _userRepository = userRepository;
        _notificationService = notificationService;
        _serviceCompletionTermRepository = serviceCompletionTermRepository ?? NullServiceCompletionTermRepository.Instance;
        _appointmentReminderService = appointmentReminderService ?? NullAppointmentReminderService.Instance;
        _serviceAppointmentChecklistService = serviceAppointmentChecklistService ?? NullAppointmentChecklistService.Instance;
        _scopeChangeRequestRepository = scopeChangeRequestRepository ?? NullServiceScopeChangeRequestRepository.Instance;
        _serviceRequestCommercialValueService = serviceRequestCommercialValueService ?? NullServiceRequestCommercialValueService.Instance;
        _serviceFinancialPolicyCalculationService = serviceFinancialPolicyCalculationService;
        _providerCreditService = providerCreditService;
        _scopeChangePolicies = BuildScopeChangePolicies(configuration);
        _availabilityTimeZone = ResolveAvailabilityTimeZone(configuration["ServiceAppointments:AvailabilityTimeZoneId"]);

        _providerConfirmationExpiryHours = ParsePolicyValue(
            configuration,
            "ServiceAppointments:ConfirmationExpiryHours",
            defaultValue: 12,
            minimum: 1,
            maximum: 72);

        _cancelMinimumHoursBeforeWindow = ParsePolicyValue(
            configuration,
            "ServiceAppointments:CancelMinimumHoursBeforeWindow",
            defaultValue: 2,
            minimum: 0,
            maximum: 72);

        _rescheduleMinimumHoursBeforeWindow = ParsePolicyValue(
            configuration,
            "ServiceAppointments:RescheduleMinimumHoursBeforeWindow",
            defaultValue: 2,
            minimum: 0,
            maximum: 72);

        _rescheduleMaximumAdvanceDays = ParsePolicyValue(
            configuration,
            "ServiceAppointments:RescheduleMaximumAdvanceDays",
            defaultValue: 30,
            minimum: 1,
            maximum: 365);

        _completionPinLength = ParsePolicyValue(
            configuration,
            "ServiceAppointments:CompletionPinLength",
            defaultValue: 6,
            minimum: 4,
            maximum: 8);

        _completionPinExpiryMinutes = ParsePolicyValue(
            configuration,
            "ServiceAppointments:CompletionPinExpiryMinutes",
            defaultValue: 30,
            minimum: 5,
            maximum: 24 * 60);

        _completionPinMaxFailedAttempts = ParsePolicyValue(
            configuration,
            "ServiceAppointments:CompletionPinMaxFailedAttempts",
            defaultValue: 5,
            minimum: 1,
            maximum: 20);

        _scopeChangeClientApprovalTimeoutMinutes = ParsePolicyValue(
            configuration,
            "ServiceAppointments:ScopeChanges:ClientApprovalTimeoutMinutes",
            defaultValue: 1440,
            minimum: 5,
            maximum: 7 * 24 * 60);
    }

    public async Task<ServiceAppointmentSlotsResultDto> GetAvailableSlotsAsync(
        Guid actorUserId,
        string actorRole,
        GetServiceAppointmentSlotsQueryDto query)
    {
        var rangeStartUtc = NormalizeToUtc(query.FromUtc);
        var rangeEndUtc = NormalizeToUtc(query.ToUtc);

        if (query.ProviderId == Guid.Empty)
        {
            return new ServiceAppointmentSlotsResultDto(false, Array.Empty<ServiceAppointmentSlotDto>(), "invalid_provider", "Prestador invalido.");
        }

        if (rangeEndUtc <= rangeStartUtc)
        {
            return new ServiceAppointmentSlotsResultDto(false, Array.Empty<ServiceAppointmentSlotDto>(), "invalid_range", "Intervalo de datas invalido.");
        }

        if ((rangeEndUtc - rangeStartUtc).TotalDays > MaximumSlotsQueryRangeDays)
        {
            return new ServiceAppointmentSlotsResultDto(
                false,
                Array.Empty<ServiceAppointmentSlotDto>(),
                "range_too_large",
                $"A consulta de slots permite no maximo {MaximumSlotsQueryRangeDays} dias.");
        }

        if (query.SlotDurationMinutes.HasValue &&
            (query.SlotDurationMinutes.Value < MinimumSlotDurationMinutes ||
             query.SlotDurationMinutes.Value > MaximumSlotDurationMinutes))
        {
            return new ServiceAppointmentSlotsResultDto(
                false,
                Array.Empty<ServiceAppointmentSlotDto>(),
                "invalid_slot_duration",
                $"Duracao de slot deve estar entre {MinimumSlotDurationMinutes} e {MaximumSlotDurationMinutes} minutos.");
        }

        if (!IsAdminRole(actorRole) && !IsClientRole(actorRole) && !IsProviderRole(actorRole))
        {
            return new ServiceAppointmentSlotsResultDto(false, Array.Empty<ServiceAppointmentSlotDto>(), "forbidden", "Perfil sem permissao para consultar slots.");
        }

        if (IsProviderRole(actorRole) && actorUserId != query.ProviderId)
        {
            return new ServiceAppointmentSlotsResultDto(false, Array.Empty<ServiceAppointmentSlotDto>(), "forbidden", "Prestador so pode consultar seus proprios slots.");
        }

        var provider = await _userRepository.GetByIdAsync(query.ProviderId);
        if (provider == null || provider.Role != UserRole.Provider || !provider.IsActive)
        {
            return new ServiceAppointmentSlotsResultDto(false, Array.Empty<ServiceAppointmentSlotDto>(), "provider_not_found", "Prestador nao encontrado.");
        }

        var rules = await _serviceAppointmentRepository.GetAvailabilityRulesByProviderAsync(query.ProviderId);
        if (rules.Count == 0)
        {
            return new ServiceAppointmentSlotsResultDto(true, Array.Empty<ServiceAppointmentSlotDto>());
        }

        var exceptions = await _serviceAppointmentRepository.GetAvailabilityExceptionsByProviderAsync(
            query.ProviderId,
            rangeStartUtc,
            rangeEndUtc);

        var reservedAppointments = await _serviceAppointmentRepository.GetProviderAppointmentsByStatusesInRangeAsync(
            query.ProviderId,
            rangeStartUtc,
            rangeEndUtc,
            BlockingStatuses);

        var slots = BuildAvailableSlots(
            rules,
            exceptions,
            reservedAppointments,
            rangeStartUtc,
            rangeEndUtc,
            query.SlotDurationMinutes);

        return new ServiceAppointmentSlotsResultDto(true, slots);
    }

    public async Task<ProviderAvailabilityOverviewResultDto> GetProviderAvailabilityOverviewAsync(
        Guid actorUserId,
        string actorRole,
        Guid providerId)
    {
        if (!IsAdminRole(actorRole) && !IsProviderRole(actorRole))
        {
            return new ProviderAvailabilityOverviewResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Perfil sem permissao para consultar disponibilidade.");
        }

        if (providerId == Guid.Empty)
        {
            return new ProviderAvailabilityOverviewResultDto(false, ErrorCode: "invalid_provider", ErrorMessage: "Prestador invalido.");
        }

        if (IsProviderRole(actorRole) && actorUserId != providerId)
        {
            return new ProviderAvailabilityOverviewResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Prestador so pode consultar sua propria disponibilidade.");
        }

        var provider = await _userRepository.GetByIdAsync(providerId);
        if (provider == null || provider.Role != UserRole.Provider || !provider.IsActive)
        {
            return new ProviderAvailabilityOverviewResultDto(false, ErrorCode: "provider_not_found", ErrorMessage: "Prestador nao encontrado.");
        }

        var rules = await _serviceAppointmentRepository.GetAvailabilityRulesByProviderAsync(providerId);
        var exceptions = await _serviceAppointmentRepository.GetAvailabilityExceptionsByProviderAsync(providerId);
        var activeBlocks = exceptions
            .Where(e => e.EndsAtUtc >= DateTime.UtcNow.AddDays(-1))
            .OrderBy(e => e.StartsAtUtc)
            .ToList();

        return new ProviderAvailabilityOverviewResultDto(
            true,
            new ProviderAvailabilityOverviewDto(
                providerId,
                rules.Select(MapAvailabilityRuleToDto).ToList(),
                activeBlocks.Select(MapAvailabilityExceptionToDto).ToList()));
    }

    public async Task<ProviderAvailabilityOperationResultDto> AddProviderAvailabilityRuleAsync(
        Guid actorUserId,
        string actorRole,
        CreateProviderAvailabilityRuleRequestDto request)
    {
        if (!IsAdminRole(actorRole) && !IsProviderRole(actorRole))
        {
            return new ProviderAvailabilityOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Perfil sem permissao para cadastrar regra de disponibilidade.");
        }

        if (request.ProviderId == Guid.Empty)
        {
            return new ProviderAvailabilityOperationResultDto(false, ErrorCode: "invalid_provider", ErrorMessage: "Prestador invalido.");
        }

        if (IsProviderRole(actorRole) && actorUserId != request.ProviderId)
        {
            return new ProviderAvailabilityOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Prestador so pode cadastrar regras para si.");
        }

        if (request.EndTime <= request.StartTime)
        {
            return new ProviderAvailabilityOperationResultDto(false, ErrorCode: "invalid_window", ErrorMessage: "Horario final deve ser maior que horario inicial.");
        }

        if (request.SlotDurationMinutes < MinimumSlotDurationMinutes || request.SlotDurationMinutes > MaximumSlotDurationMinutes)
        {
            return new ProviderAvailabilityOperationResultDto(
                false,
                ErrorCode: "invalid_slot_duration",
                ErrorMessage: $"Duracao de slot deve estar entre {MinimumSlotDurationMinutes} e {MaximumSlotDurationMinutes} minutos.");
        }

        var provider = await _userRepository.GetByIdAsync(request.ProviderId);
        if (provider == null || provider.Role != UserRole.Provider || !provider.IsActive)
        {
            return new ProviderAvailabilityOperationResultDto(false, ErrorCode: "provider_not_found", ErrorMessage: "Prestador nao encontrado.");
        }

        var existingRules = await _serviceAppointmentRepository.GetAvailabilityRulesByProviderAsync(request.ProviderId);
        var hasOverlap = existingRules.Any(r =>
            r.DayOfWeek == request.DayOfWeek &&
            r.StartTime < request.EndTime &&
            r.EndTime > request.StartTime);

        if (hasOverlap)
        {
            return new ProviderAvailabilityOperationResultDto(false, ErrorCode: "rule_overlap", ErrorMessage: "Ja existe uma regra sobreposta para esse dia e horario.");
        }

        await _serviceAppointmentRepository.AddAvailabilityRuleAsync(new ProviderAvailabilityRule
        {
            ProviderId = request.ProviderId,
            DayOfWeek = request.DayOfWeek,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            SlotDurationMinutes = request.SlotDurationMinutes,
            IsActive = true
        });

        return new ProviderAvailabilityOperationResultDto(true);
    }

    public async Task<ProviderAvailabilityOperationResultDto> RemoveProviderAvailabilityRuleAsync(
        Guid actorUserId,
        string actorRole,
        Guid ruleId)
    {
        if (!IsAdminRole(actorRole) && !IsProviderRole(actorRole))
        {
            return new ProviderAvailabilityOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Perfil sem permissao para remover regra de disponibilidade.");
        }

        if (ruleId == Guid.Empty)
        {
            return new ProviderAvailabilityOperationResultDto(false, ErrorCode: "invalid_rule", ErrorMessage: "Regra invalida.");
        }

        var rule = await _serviceAppointmentRepository.GetAvailabilityRuleByIdAsync(ruleId);
        if (rule == null)
        {
            return new ProviderAvailabilityOperationResultDto(false, ErrorCode: "rule_not_found", ErrorMessage: "Regra nao encontrada.");
        }

        if (IsProviderRole(actorRole) && actorUserId != rule.ProviderId)
        {
            return new ProviderAvailabilityOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Prestador so pode remover suas proprias regras.");
        }

        if (!rule.IsActive)
        {
            return new ProviderAvailabilityOperationResultDto(true);
        }

        rule.IsActive = false;
        rule.UpdatedAt = DateTime.UtcNow;
        await _serviceAppointmentRepository.UpdateAvailabilityRuleAsync(rule);
        return new ProviderAvailabilityOperationResultDto(true);
    }

    public async Task<ProviderAvailabilityOperationResultDto> AddProviderAvailabilityExceptionAsync(
        Guid actorUserId,
        string actorRole,
        CreateProviderAvailabilityExceptionRequestDto request)
    {
        if (!IsAdminRole(actorRole) && !IsProviderRole(actorRole))
        {
            return new ProviderAvailabilityOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Perfil sem permissao para cadastrar bloqueio.");
        }

        if (request.ProviderId == Guid.Empty)
        {
            return new ProviderAvailabilityOperationResultDto(false, ErrorCode: "invalid_provider", ErrorMessage: "Prestador invalido.");
        }

        if (IsProviderRole(actorRole) && actorUserId != request.ProviderId)
        {
            return new ProviderAvailabilityOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Prestador so pode cadastrar bloqueios para si.");
        }

        var startsAtUtc = NormalizeToUtc(request.StartsAtUtc);
        var endsAtUtc = NormalizeToUtc(request.EndsAtUtc);
        if (endsAtUtc <= startsAtUtc)
        {
            return new ProviderAvailabilityOperationResultDto(false, ErrorCode: "invalid_window", ErrorMessage: "Periodo de bloqueio invalido.");
        }

        var provider = await _userRepository.GetByIdAsync(request.ProviderId);
        if (provider == null || provider.Role != UserRole.Provider || !provider.IsActive)
        {
            return new ProviderAvailabilityOperationResultDto(false, ErrorCode: "provider_not_found", ErrorMessage: "Prestador nao encontrado.");
        }

        var existingExceptions = await _serviceAppointmentRepository.GetAvailabilityExceptionsByProviderAsync(request.ProviderId);
        var hasOverlap = existingExceptions.Any(e => Overlaps(startsAtUtc, endsAtUtc, e.StartsAtUtc, e.EndsAtUtc));
        if (hasOverlap)
        {
            return new ProviderAvailabilityOperationResultDto(false, ErrorCode: "block_overlap", ErrorMessage: "Ja existe um bloqueio sobreposto nesse periodo.");
        }

        var conflictingAppointments = await _serviceAppointmentRepository.GetProviderAppointmentsByStatusesInRangeAsync(
            request.ProviderId,
            startsAtUtc,
            endsAtUtc,
            BlockingStatuses);
        if (conflictingAppointments.Any())
        {
            return new ProviderAvailabilityOperationResultDto(
                false,
                ErrorCode: "block_conflict_appointment",
                ErrorMessage: "Existe agendamento ativo dentro do periodo informado.");
        }

        await _serviceAppointmentRepository.AddAvailabilityExceptionAsync(new ProviderAvailabilityException
        {
            ProviderId = request.ProviderId,
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = endsAtUtc,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            IsActive = true
        });

        return new ProviderAvailabilityOperationResultDto(true);
    }

    public async Task<ProviderAvailabilityOperationResultDto> RemoveProviderAvailabilityExceptionAsync(
        Guid actorUserId,
        string actorRole,
        Guid exceptionId)
    {
        if (!IsAdminRole(actorRole) && !IsProviderRole(actorRole))
        {
            return new ProviderAvailabilityOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Perfil sem permissao para remover bloqueio.");
        }

        if (exceptionId == Guid.Empty)
        {
            return new ProviderAvailabilityOperationResultDto(false, ErrorCode: "invalid_block", ErrorMessage: "Bloqueio invalido.");
        }

        var exception = await _serviceAppointmentRepository.GetAvailabilityExceptionByIdAsync(exceptionId);
        if (exception == null)
        {
            return new ProviderAvailabilityOperationResultDto(false, ErrorCode: "block_not_found", ErrorMessage: "Bloqueio nao encontrado.");
        }

        if (IsProviderRole(actorRole) && actorUserId != exception.ProviderId)
        {
            return new ProviderAvailabilityOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Prestador so pode remover seus proprios bloqueios.");
        }

        if (!exception.IsActive)
        {
            return new ProviderAvailabilityOperationResultDto(true);
        }

        exception.IsActive = false;
        exception.UpdatedAt = DateTime.UtcNow;
        await _serviceAppointmentRepository.UpdateAvailabilityExceptionAsync(exception);
        return new ProviderAvailabilityOperationResultDto(true);
    }

    public async Task<ServiceAppointmentOperationResultDto> CreateAsync(
        Guid actorUserId,
        string actorRole,
        CreateServiceAppointmentRequestDto request)
    {
        if (!IsAdminRole(actorRole) && !IsClientRole(actorRole) && !IsProviderRole(actorRole))
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Perfil sem permissao para criar agendamento.");
        }

        var windowStartUtc = NormalizeToUtc(request.WindowStartUtc);
        var windowEndUtc = NormalizeToUtc(request.WindowEndUtc);

        if (request.ServiceRequestId == Guid.Empty)
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "invalid_request", ErrorMessage: "Pedido invalido.");
        }

        if (request.ProviderId == Guid.Empty)
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "invalid_provider", ErrorMessage: "Prestador invalido.");
        }

        if (windowEndUtc <= windowStartUtc)
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "invalid_window", ErrorMessage: "Janela de horario invalida.");
        }

        var windowMinutes = (windowEndUtc - windowStartUtc).TotalMinutes;
        if (windowMinutes < MinimumSlotDurationMinutes || windowMinutes > MaximumAppointmentWindowMinutes)
        {
            return new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "invalid_window",
                ErrorMessage: $"A janela deve estar entre {MinimumSlotDurationMinutes} e {MaximumAppointmentWindowMinutes} minutos.");
        }

        if (windowStartUtc.Date != windowEndUtc.Date)
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "invalid_window", ErrorMessage: "A janela deve estar no mesmo dia.");
        }

        if (windowStartUtc < DateTime.UtcNow.AddMinutes(1))
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "invalid_window", ErrorMessage: "O horario do agendamento deve ser futuro.");
        }

        var provider = await _userRepository.GetByIdAsync(request.ProviderId);
        if (provider == null || provider.Role != UserRole.Provider || !provider.IsActive)
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "provider_not_found", ErrorMessage: "Prestador nao encontrado.");
        }

        if (IsProviderRole(actorRole) && actorUserId != request.ProviderId)
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Prestador so pode criar agendamento para si.");
        }

        var serviceRequest = await _serviceRequestRepository.GetByIdAsync(request.ServiceRequestId);
        if (serviceRequest == null)
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "request_not_found", ErrorMessage: "Pedido nao encontrado.");
        }

        if (IsClientRole(actorRole) && serviceRequest.ClientId != actorUserId)
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Cliente nao pode agendar pedido de outro cliente.");
        }

        if (IsServiceRequestClosedForScheduling(serviceRequest.Status))
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "request_closed", ErrorMessage: "Pedido ja finalizado/cancelado.");
        }

        var hasAcceptedProviderProposal = serviceRequest.Proposals.Any(p =>
            p.ProviderId == request.ProviderId &&
            p.Accepted &&
            !p.IsInvalidated);

        if (!hasAcceptedProviderProposal)
        {
            return new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "provider_not_assigned",
                ErrorMessage: "Prestador nao possui proposta aceita para este pedido.");
        }

        var lockDateKey = windowStartUtc.ToString("yyyyMMdd");
        var lockKeys = new[]
        {
            $"provider:{request.ProviderId:N}:{lockDateKey}",
            $"request:{request.ServiceRequestId:N}:{lockDateKey}"
        };
        var lockInstances = await AcquireCreationLocksAsync(lockKeys);

        try
        {
            var existingAppointments = await _serviceAppointmentRepository.GetByRequestIdAsync(request.ServiceRequestId)
                ?? Array.Empty<ServiceAppointment>();
            var hasRequestWindowConflict = existingAppointments.Any(a =>
                IsSchedulingActiveStatus(a.Status) &&
                Overlaps(windowStartUtc, windowEndUtc, a.WindowStartUtc, a.WindowEndUtc));

            if (hasRequestWindowConflict)
            {
                return new ServiceAppointmentOperationResultDto(
                    false,
                    ErrorCode: "request_window_conflict",
                    ErrorMessage: "Este pedido ja possui agendamento ativo nesse horario.");
            }

            var slotAvailable = await IsSlotAvailableForProviderAsync(request.ProviderId, windowStartUtc, windowEndUtc);
            if (!slotAvailable)
            {
                return new ServiceAppointmentOperationResultDto(
                    false,
                    ErrorCode: "slot_unavailable",
                    ErrorMessage: "A janela escolhida nao esta disponivel para o prestador.");
            }

            var appointment = new ServiceAppointment
            {
                ServiceRequestId = request.ServiceRequestId,
                ClientId = serviceRequest.ClientId,
                ProviderId = request.ProviderId,
                WindowStartUtc = windowStartUtc,
                WindowEndUtc = windowEndUtc,
                ExpiresAtUtc = DateTime.UtcNow.AddHours(_providerConfirmationExpiryHours),
                Status = ServiceAppointmentStatus.PendingProviderConfirmation,
                Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim()
            };

            await _serviceAppointmentRepository.AddAsync(appointment);

            await _serviceAppointmentRepository.AddHistoryAsync(new ServiceAppointmentHistory
            {
                ServiceAppointmentId = appointment.Id,
                PreviousStatus = null,
                NewStatus = ServiceAppointmentStatus.PendingProviderConfirmation,
                ActorUserId = actorUserId,
                ActorRole = ResolveActorRole(actorRole),
                Reason = "Agendamento criado."
            });

            await SyncServiceRequestSchedulingStatusAsync(serviceRequest);

            await _notificationService.SendNotificationAsync(
                serviceRequest.ClientId.ToString("N"),
                "Agendamento solicitado",
                "Seu agendamento foi criado e esta aguardando confirmacao do prestador.",
                BuildActionUrl(serviceRequest.Id));

            await _notificationService.SendNotificationAsync(
                request.ProviderId.ToString("N"),
                "Novo agendamento pendente",
                "Voce possui um agendamento pendente para confirmacao.",
                BuildActionUrl(serviceRequest.Id));

            var loaded = await _serviceAppointmentRepository.GetByIdAsync(appointment.Id) ?? appointment;
            return new ServiceAppointmentOperationResultDto(true, MapToDto(loaded));
        }
        finally
        {
            ReleaseCreationLocks(lockInstances);
        }
    }

    public async Task<ServiceAppointmentOperationResultDto> ConfirmAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId)
    {
        if (!IsProviderRole(actorRole) && !IsAdminRole(actorRole))
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Perfil sem permissao para confirmar agendamento.");
        }

        var appointment = await _serviceAppointmentRepository.GetByIdAsync(appointmentId);
        if (appointment == null)
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "appointment_not_found", ErrorMessage: "Agendamento nao encontrado.");
        }

        if (!IsAdminRole(actorRole) && appointment.ProviderId != actorUserId)
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Prestador nao pode confirmar agendamento de outro prestador.");
        }

        if (appointment.Status == ServiceAppointmentStatus.Confirmed)
        {
            return new ServiceAppointmentOperationResultDto(true, MapToDto(appointment));
        }

        if (appointment.Status != ServiceAppointmentStatus.PendingProviderConfirmation)
        {
            return new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "invalid_state",
                ErrorMessage: $"Nao e possivel confirmar agendamento no status {appointment.Status}.");
        }

        var nowUtc = DateTime.UtcNow;
        var previousStatus = appointment.Status;
        var previousOperationalStatus = ResolveCurrentOperationalStatus(appointment);
        appointment.Status = ServiceAppointmentStatus.Confirmed;
        appointment.OperationalStatus = ServiceAppointmentOperationalStatus.OnTheWay;
        appointment.OperationalStatusUpdatedAtUtc = nowUtc;
        appointment.OperationalStatusReason = "Prestador confirmou o agendamento.";
        appointment.ConfirmedAtUtc = nowUtc;
        appointment.ExpiresAtUtc = null;
        appointment.UpdatedAt = nowUtc;
        await _serviceAppointmentRepository.UpdateAsync(appointment);

        await _serviceAppointmentRepository.AddHistoryAsync(new ServiceAppointmentHistory
        {
            ServiceAppointmentId = appointment.Id,
            PreviousStatus = previousStatus,
            NewStatus = ServiceAppointmentStatus.Confirmed,
            PreviousOperationalStatus = previousOperationalStatus,
            NewOperationalStatus = ServiceAppointmentOperationalStatus.OnTheWay,
            ActorUserId = actorUserId,
            ActorRole = ResolveActorRole(actorRole),
            Reason = "Agendamento confirmado pelo prestador."
        });

        await SyncServiceRequestSchedulingStatusAsync(appointment.ServiceRequest);

        await _notificationService.SendNotificationAsync(
            appointment.ClientId.ToString("N"),
            "Agendamento confirmado",
            "Seu agendamento foi confirmado pelo prestador.",
            BuildActionUrl(appointment.ServiceRequestId));

        await _notificationService.SendNotificationAsync(
            appointment.ProviderId.ToString("N"),
            "Agendamento confirmado",
            "Voce confirmou o agendamento com sucesso.",
            BuildActionUrl(appointment.ServiceRequestId));

        await _appointmentReminderService.ScheduleForAppointmentAsync(
            appointment.Id,
            "agendamento_confirmado");

        await SyncServiceRequestSchedulingStatusAsync(appointment.ServiceRequest);

        var loaded = await _serviceAppointmentRepository.GetByIdAsync(appointment.Id) ?? appointment;
        return new ServiceAppointmentOperationResultDto(true, MapToDto(loaded));
    }

    public async Task<ServiceAppointmentOperationResultDto> RejectAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        RejectServiceAppointmentRequestDto request)
    {
        if (!IsProviderRole(actorRole) && !IsAdminRole(actorRole))
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Perfil sem permissao para recusar agendamento.");
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "invalid_reason", ErrorMessage: "Motivo da recusa e obrigatorio.");
        }

        var appointment = await _serviceAppointmentRepository.GetByIdAsync(appointmentId);
        if (appointment == null)
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "appointment_not_found", ErrorMessage: "Agendamento nao encontrado.");
        }

        if (!IsAdminRole(actorRole) && appointment.ProviderId != actorUserId)
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Prestador nao pode recusar agendamento de outro prestador.");
        }

        if (appointment.Status == ServiceAppointmentStatus.RejectedByProvider)
        {
            return new ServiceAppointmentOperationResultDto(true, MapToDto(appointment));
        }

        if (appointment.Status != ServiceAppointmentStatus.PendingProviderConfirmation)
        {
            return new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "invalid_state",
                ErrorMessage: $"Nao e possivel recusar agendamento no status {appointment.Status}.");
        }

        var nowUtc = DateTime.UtcNow;
        var previousStatus = appointment.Status;
        appointment.Status = ServiceAppointmentStatus.RejectedByProvider;
        appointment.RejectedAtUtc = nowUtc;
        appointment.ExpiresAtUtc = null;
        appointment.Reason = reason;
        appointment.UpdatedAt = nowUtc;
        await _serviceAppointmentRepository.UpdateAsync(appointment);

        await _serviceAppointmentRepository.AddHistoryAsync(new ServiceAppointmentHistory
        {
            ServiceAppointmentId = appointment.Id,
            PreviousStatus = previousStatus,
            NewStatus = ServiceAppointmentStatus.RejectedByProvider,
            ActorUserId = actorUserId,
            ActorRole = ResolveActorRole(actorRole),
            Reason = reason
        });

        await SyncServiceRequestSchedulingStatusAsync(appointment.ServiceRequest);

        await _notificationService.SendNotificationAsync(
            appointment.ClientId.ToString("N"),
            "Agendamento recusado",
            $"O prestador recusou o agendamento. Motivo: {reason}",
            BuildActionUrl(appointment.ServiceRequestId));

        await _notificationService.SendNotificationAsync(
            appointment.ProviderId.ToString("N"),
            "Agendamento recusado",
            "Voce recusou o agendamento.",
            BuildActionUrl(appointment.ServiceRequestId));

        var loaded = await _serviceAppointmentRepository.GetByIdAsync(appointment.Id) ?? appointment;
        return new ServiceAppointmentOperationResultDto(true, MapToDto(loaded));
    }

    public async Task<ServiceAppointmentOperationResultDto> RequestRescheduleAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        RequestServiceAppointmentRescheduleDto request)
    {
        if (!IsClientRole(actorRole) && !IsProviderRole(actorRole))
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Perfil sem permissao para solicitar reagendamento.");
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "invalid_reason", ErrorMessage: "Motivo do reagendamento e obrigatorio.");
        }

        var proposedWindowStartUtc = NormalizeToUtc(request.ProposedWindowStartUtc);
        var proposedWindowEndUtc = NormalizeToUtc(request.ProposedWindowEndUtc);
        if (proposedWindowEndUtc <= proposedWindowStartUtc)
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "invalid_window", ErrorMessage: "Janela proposta invalida.");
        }

        var proposedWindowMinutes = (proposedWindowEndUtc - proposedWindowStartUtc).TotalMinutes;
        if (proposedWindowMinutes < MinimumSlotDurationMinutes || proposedWindowMinutes > MaximumAppointmentWindowMinutes)
        {
            return new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "invalid_window",
                ErrorMessage: $"A janela deve estar entre {MinimumSlotDurationMinutes} e {MaximumAppointmentWindowMinutes} minutos.");
        }

        if (proposedWindowStartUtc.Date != proposedWindowEndUtc.Date)
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "invalid_window", ErrorMessage: "A janela deve estar no mesmo dia.");
        }

        var nowUtc = DateTime.UtcNow;
        if (proposedWindowStartUtc < nowUtc.AddMinutes(1))
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "invalid_window", ErrorMessage: "A nova janela deve ser futura.");
        }

        if (proposedWindowStartUtc > nowUtc.AddDays(_rescheduleMaximumAdvanceDays))
        {
            return new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "policy_violation",
                ErrorMessage: $"A nova janela pode ser definida com no maximo {_rescheduleMaximumAdvanceDays} dias de antecedencia.");
        }

        var appointment = await _serviceAppointmentRepository.GetByIdAsync(appointmentId);
        if (appointment == null)
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "appointment_not_found", ErrorMessage: "Agendamento nao encontrado.");
        }

        if (!CanAccessAppointment(appointment, actorUserId, actorRole))
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Sem permissao para solicitar reagendamento deste agendamento.");
        }

        if (appointment.Status != ServiceAppointmentStatus.Confirmed &&
            appointment.Status != ServiceAppointmentStatus.RescheduleConfirmed)
        {
            return new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "invalid_state",
                ErrorMessage: $"Nao e possivel solicitar reagendamento no status {appointment.Status}.");
        }

        if (appointment.WindowStartUtc <= nowUtc.AddHours(_rescheduleMinimumHoursBeforeWindow))
        {
            return new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "policy_violation",
                ErrorMessage: $"Reagendamento exige no minimo {_rescheduleMinimumHoursBeforeWindow} horas de antecedencia.");
        }

        if (appointment.WindowStartUtc == proposedWindowStartUtc && appointment.WindowEndUtc == proposedWindowEndUtc)
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "invalid_window", ErrorMessage: "A nova janela deve ser diferente da atual.");
        }

        var slotAvailable = await IsSlotAvailableForProviderAsync(
            appointment.ProviderId,
            proposedWindowStartUtc,
            proposedWindowEndUtc,
            excludedAppointmentId: appointment.Id);

        if (!slotAvailable)
        {
            return new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "slot_unavailable",
                ErrorMessage: "A janela proposta nao esta disponivel para o prestador.");
        }

        var previousStatus = appointment.Status;
        var isRequesterClient = IsClientRole(actorRole);
        appointment.Status = isRequesterClient
            ? ServiceAppointmentStatus.RescheduleRequestedByClient
            : ServiceAppointmentStatus.RescheduleRequestedByProvider;
        appointment.ProposedWindowStartUtc = proposedWindowStartUtc;
        appointment.ProposedWindowEndUtc = proposedWindowEndUtc;
        appointment.RescheduleRequestedAtUtc = nowUtc;
        appointment.RescheduleRequestedByRole = ResolveActorRole(actorRole);
        appointment.RescheduleRequestReason = reason;
        appointment.UpdatedAt = nowUtc;
        await _serviceAppointmentRepository.UpdateAsync(appointment);

        await _serviceAppointmentRepository.AddHistoryAsync(new ServiceAppointmentHistory
        {
            ServiceAppointmentId = appointment.Id,
            PreviousStatus = previousStatus,
            NewStatus = appointment.Status,
            ActorUserId = actorUserId,
            ActorRole = ResolveActorRole(actorRole),
            Reason = reason,
            Metadata = $"ProposedWindowStartUtc={proposedWindowStartUtc:O};ProposedWindowEndUtc={proposedWindowEndUtc:O}"
        });

        var targetUserId = isRequesterClient ? appointment.ProviderId : appointment.ClientId;
        var targetLabel = isRequesterClient ? "prestador" : "cliente";
        await _notificationService.SendNotificationAsync(
            targetUserId.ToString("N"),
            "Solicitacao de reagendamento",
            $"O {targetLabel} recebeu uma solicitacao de reagendamento para {proposedWindowStartUtc:dd/MM HH:mm} - {proposedWindowEndUtc:HH:mm}.",
            BuildActionUrl(appointment.ServiceRequestId));

        await _notificationService.SendNotificationAsync(
            actorUserId.ToString("N"),
            "Reagendamento solicitado",
            "Sua solicitacao de reagendamento foi enviada e esta aguardando resposta.",
            BuildActionUrl(appointment.ServiceRequestId));

        var loaded = await _serviceAppointmentRepository.GetByIdAsync(appointment.Id) ?? appointment;
        return new ServiceAppointmentOperationResultDto(true, MapToDto(loaded));
    }

    public async Task<ServiceAppointmentOperationResultDto> RespondRescheduleAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        RespondServiceAppointmentRescheduleRequestDto request)
    {
        if (!IsClientRole(actorRole) && !IsProviderRole(actorRole))
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Perfil sem permissao para responder reagendamento.");
        }

        var appointment = await _serviceAppointmentRepository.GetByIdAsync(appointmentId);
        if (appointment == null)
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "appointment_not_found", ErrorMessage: "Agendamento nao encontrado.");
        }

        if (!CanAccessAppointment(appointment, actorUserId, actorRole))
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Sem permissao para responder este reagendamento.");
        }

        var isClientResponder = IsClientRole(actorRole);
        var isProviderResponder = IsProviderRole(actorRole);
        if (appointment.Status == ServiceAppointmentStatus.RescheduleRequestedByClient && !isProviderResponder)
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Somente o prestador pode responder este reagendamento.");
        }

        if (appointment.Status == ServiceAppointmentStatus.RescheduleRequestedByProvider && !isClientResponder)
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Somente o cliente pode responder este reagendamento.");
        }

        if (appointment.Status != ServiceAppointmentStatus.RescheduleRequestedByClient &&
            appointment.Status != ServiceAppointmentStatus.RescheduleRequestedByProvider)
        {
            return new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "invalid_state",
                ErrorMessage: $"Nao ha solicitacao de reagendamento pendente para o status {appointment.Status}.");
        }

        if (!appointment.ProposedWindowStartUtc.HasValue || !appointment.ProposedWindowEndUtc.HasValue)
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "invalid_state", ErrorMessage: "Solicitacao de reagendamento inconsistente.");
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        var nowUtc = DateTime.UtcNow;
        var previousStatus = appointment.Status;
        var previousOperationalStatus = ResolveCurrentOperationalStatus(appointment);
        if (request.Accept)
        {
            var proposedStartUtc = appointment.ProposedWindowStartUtc.Value;
            var proposedEndUtc = appointment.ProposedWindowEndUtc.Value;
            var slotAvailable = await IsSlotAvailableForProviderAsync(
                appointment.ProviderId,
                proposedStartUtc,
                proposedEndUtc,
                excludedAppointmentId: appointment.Id);

            if (!slotAvailable)
            {
                return new ServiceAppointmentOperationResultDto(
                    false,
                    ErrorCode: "slot_unavailable",
                    ErrorMessage: "A janela proposta nao esta mais disponivel.");
            }

            appointment.WindowStartUtc = proposedStartUtc;
            appointment.WindowEndUtc = proposedEndUtc;
            appointment.Status = ServiceAppointmentStatus.RescheduleConfirmed;
            appointment.Reason = string.IsNullOrWhiteSpace(reason)
                ? appointment.RescheduleRequestReason
                : reason;
            appointment.OperationalStatus = ServiceAppointmentOperationalStatus.OnTheWay;
            appointment.OperationalStatusUpdatedAtUtc = nowUtc;
            appointment.OperationalStatusReason = "Reagendamento confirmado.";
            appointment.ConfirmedAtUtc ??= nowUtc;
            ClearRescheduleProposal(appointment);
            appointment.UpdatedAt = nowUtc;
            await _serviceAppointmentRepository.UpdateAsync(appointment);

            await _serviceAppointmentRepository.AddHistoryAsync(new ServiceAppointmentHistory
            {
                ServiceAppointmentId = appointment.Id,
                PreviousStatus = previousStatus,
                NewStatus = ServiceAppointmentStatus.RescheduleConfirmed,
                PreviousOperationalStatus = previousOperationalStatus,
                NewOperationalStatus = ServiceAppointmentOperationalStatus.OnTheWay,
                ActorUserId = actorUserId,
                ActorRole = ResolveActorRole(actorRole),
                Reason = appointment.Reason
            });

            await _notificationService.SendNotificationAsync(
                appointment.ClientId.ToString("N"),
                "Reagendamento confirmado",
                "O reagendamento foi aceito e a nova janela foi aplicada.",
                BuildActionUrl(appointment.ServiceRequestId));

            await _notificationService.SendNotificationAsync(
                appointment.ProviderId.ToString("N"),
                "Reagendamento confirmado",
                "O reagendamento foi aceito e a nova janela foi aplicada.",
                BuildActionUrl(appointment.ServiceRequestId));

            await _appointmentReminderService.ScheduleForAppointmentAsync(
                appointment.Id,
                "reagendamento_confirmado");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return new ServiceAppointmentOperationResultDto(false, ErrorCode: "invalid_reason", ErrorMessage: "Motivo da recusa e obrigatorio.");
            }

            var statusBeforeRequest = ResolveStatusBeforePendingReschedule(appointment);
            appointment.Status = statusBeforeRequest;
            appointment.Reason = reason;
            ClearRescheduleProposal(appointment);
            appointment.UpdatedAt = nowUtc;
            await _serviceAppointmentRepository.UpdateAsync(appointment);

            await _serviceAppointmentRepository.AddHistoryAsync(new ServiceAppointmentHistory
            {
                ServiceAppointmentId = appointment.Id,
                PreviousStatus = previousStatus,
                NewStatus = statusBeforeRequest,
                PreviousOperationalStatus = previousOperationalStatus,
                NewOperationalStatus = ResolveCurrentOperationalStatus(appointment),
                ActorUserId = actorUserId,
                ActorRole = ResolveActorRole(actorRole),
                Reason = reason
            });

            var requesterId = previousStatus == ServiceAppointmentStatus.RescheduleRequestedByClient
                ? appointment.ClientId
                : appointment.ProviderId;
            await _notificationService.SendNotificationAsync(
                requesterId.ToString("N"),
                "Reagendamento recusado",
                $"Sua solicitacao de reagendamento foi recusada. Motivo: {reason}",
                BuildActionUrl(appointment.ServiceRequestId));

            await _notificationService.SendNotificationAsync(
                actorUserId.ToString("N"),
                "Reagendamento recusado",
                "Voce recusou a solicitacao de reagendamento.",
                BuildActionUrl(appointment.ServiceRequestId));
        }

        var loaded = await _serviceAppointmentRepository.GetByIdAsync(appointment.Id) ?? appointment;
        return new ServiceAppointmentOperationResultDto(true, MapToDto(loaded));
    }

    public async Task<ServiceAppointmentOperationResultDto> CancelAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        CancelServiceAppointmentRequestDto request)
    {
        if (!IsClientRole(actorRole) && !IsProviderRole(actorRole))
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Perfil sem permissao para cancelar agendamento.");
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "invalid_reason", ErrorMessage: "Motivo do cancelamento e obrigatorio.");
        }

        var appointment = await _serviceAppointmentRepository.GetByIdAsync(appointmentId);
        if (appointment == null)
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "appointment_not_found", ErrorMessage: "Agendamento nao encontrado.");
        }

        if (!CanAccessAppointment(appointment, actorUserId, actorRole))
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Sem permissao para cancelar este agendamento.");
        }

        var cancellationStatus = IsClientRole(actorRole)
            ? ServiceAppointmentStatus.CancelledByClient
            : ServiceAppointmentStatus.CancelledByProvider;

        if (appointment.Status == cancellationStatus)
        {
            return new ServiceAppointmentOperationResultDto(true, MapToDto(appointment));
        }

        if (!CanCancelFromStatus(appointment.Status))
        {
            return new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "invalid_state",
                ErrorMessage: $"Nao e possivel cancelar agendamento no status {appointment.Status}.");
        }

        var nowUtc = DateTime.UtcNow;
        if (appointment.WindowStartUtc <= nowUtc.AddHours(_cancelMinimumHoursBeforeWindow))
        {
            return new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "policy_violation",
                ErrorMessage: $"Cancelamento exige no minimo {_cancelMinimumHoursBeforeWindow} horas de antecedencia.");
        }

        var previousStatus = appointment.Status;
        appointment.Status = cancellationStatus;
        appointment.CancelledAtUtc = nowUtc;
        appointment.ExpiresAtUtc = null;
        appointment.Reason = reason;
        ClearRescheduleProposal(appointment);
        appointment.UpdatedAt = nowUtc;
        await _serviceAppointmentRepository.UpdateAsync(appointment);

        await _serviceAppointmentRepository.AddHistoryAsync(new ServiceAppointmentHistory
        {
            ServiceAppointmentId = appointment.Id,
            PreviousStatus = previousStatus,
            NewStatus = cancellationStatus,
            ActorUserId = actorUserId,
            ActorRole = ResolveActorRole(actorRole),
            Reason = reason
        });

        await SyncServiceRequestSchedulingStatusAsync(appointment.ServiceRequest);
        await ApplyFinancialPolicyForAppointmentEventAsync(
            appointment,
            IsClientRole(actorRole)
                ? ServiceFinancialPolicyEventType.ClientCancellation
                : ServiceFinancialPolicyEventType.ProviderCancellation,
            actorUserId,
            nowUtc,
            $"cancel_{actorRole}",
            reason);

        await _notificationService.SendNotificationAsync(
            appointment.ClientId.ToString("N"),
            "Agendamento cancelado",
            $"O agendamento foi cancelado. Motivo: {reason}",
            BuildActionUrl(appointment.ServiceRequestId));

        await _notificationService.SendNotificationAsync(
            appointment.ProviderId.ToString("N"),
            "Agendamento cancelado",
            $"O agendamento foi cancelado. Motivo: {reason}",
            BuildActionUrl(appointment.ServiceRequestId));

        await _appointmentReminderService.CancelPendingForAppointmentAsync(
            appointment.Id,
            $"cancelamento_{actorRole}");

        var loaded = await _serviceAppointmentRepository.GetByIdAsync(appointment.Id) ?? appointment;
        return new ServiceAppointmentOperationResultDto(true, MapToDto(loaded));
    }

    public async Task<ServiceAppointmentOperationResultDto> OverrideFinancialPolicyAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        ServiceFinancialPolicyOverrideRequestDto request)
    {
        if (!IsAdminRole(actorRole))
        {
            return new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "forbidden",
                ErrorMessage: "Perfil sem permissao para override financeiro.");
        }

        if (appointmentId == Guid.Empty)
        {
            return new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "invalid_appointment",
                ErrorMessage: "Agendamento invalido.");
        }

        if (_serviceFinancialPolicyCalculationService == null || _providerCreditService == null)
        {
            return new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "financial_policy_unavailable",
                ErrorMessage: "Fluxo financeiro indisponivel para override.");
        }

        var justification = request.Justification?.Trim();
        if (string.IsNullOrWhiteSpace(justification))
        {
            return new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "invalid_justification",
                ErrorMessage: "Justificativa obrigatoria para override administrativo.");
        }

        var eventOccurredAtUtc = request.EventOccurredAtUtc.HasValue
            ? NormalizeToUtc(request.EventOccurredAtUtc.Value)
            : DateTime.UtcNow;

        var operationLock = await AcquireAppointmentOperationalLockAsync(appointmentId);
        try
        {
            var appointment = await _serviceAppointmentRepository.GetByIdAsync(appointmentId);
            if (appointment == null)
            {
                return new ServiceAppointmentOperationResultDto(
                    false,
                    ErrorCode: "appointment_not_found",
                    ErrorMessage: "Agendamento nao encontrado.");
            }

            var currentOperationalStatus = ResolveCurrentOperationalStatus(appointment);
            await _serviceAppointmentRepository.AddHistoryAsync(new ServiceAppointmentHistory
            {
                ServiceAppointmentId = appointment.Id,
                PreviousStatus = appointment.Status,
                NewStatus = appointment.Status,
                PreviousOperationalStatus = currentOperationalStatus,
                NewOperationalStatus = currentOperationalStatus,
                ActorUserId = actorUserId,
                ActorRole = ServiceAppointmentActorRole.Admin,
                Reason = $"Override financeiro administrativo. Justificativa: {justification}",
                Metadata = JsonSerializer.Serialize(new
                {
                    type = "financial_policy_override_requested",
                    eventType = request.EventType.ToString(),
                    source = "admin_override",
                    justification,
                    eventOccurredAtUtc
                })
            });

            await ApplyFinancialPolicyForAppointmentEventAsync(
                appointment,
                request.EventType,
                actorUserId,
                eventOccurredAtUtc,
                $"admin_override_{request.EventType}",
                justification);

            await _notificationService.SendNotificationAsync(
                appointment.ClientId.ToString("N"),
                "Ajuste financeiro administrativo",
                "Um ajuste financeiro administrativo foi registrado para este agendamento.",
                BuildActionUrl(appointment.ServiceRequestId));

            await _notificationService.SendNotificationAsync(
                appointment.ProviderId.ToString("N"),
                "Ajuste financeiro administrativo",
                "Um ajuste financeiro administrativo foi registrado para este agendamento.",
                BuildActionUrl(appointment.ServiceRequestId));

            var loaded = await _serviceAppointmentRepository.GetByIdAsync(appointment.Id) ?? appointment;
            return new ServiceAppointmentOperationResultDto(true, MapToDto(loaded));
        }
        finally
        {
            operationLock.Release();
        }
    }

    public async Task<ServiceAppointmentOperationResultDto> MarkArrivedAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        MarkServiceAppointmentArrivalRequestDto request)
    {
        if (!IsProviderRole(actorRole) && !IsAdminRole(actorRole))
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Perfil sem permissao para registrar chegada.");
        }

        var operationLock = await AcquireAppointmentOperationalLockAsync(appointmentId);
        try
        {
            var appointment = await _serviceAppointmentRepository.GetByIdAsync(appointmentId);
            if (appointment == null)
            {
                return new ServiceAppointmentOperationResultDto(false, ErrorCode: "appointment_not_found", ErrorMessage: "Agendamento nao encontrado.");
            }

            if (!IsAdminRole(actorRole) && appointment.ProviderId != actorUserId)
            {
                return new ServiceAppointmentOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Prestador nao pode registrar chegada em agendamento de outro prestador.");
            }

            if (appointment.ArrivedAtUtc.HasValue)
            {
                return new ServiceAppointmentOperationResultDto(false, ErrorCode: "duplicate_checkin", ErrorMessage: "Chegada ja registrada para este agendamento.");
            }

            if (appointment.Status != ServiceAppointmentStatus.Confirmed &&
                appointment.Status != ServiceAppointmentStatus.RescheduleConfirmed)
            {
                return new ServiceAppointmentOperationResultDto(
                    false,
                    ErrorCode: "invalid_state",
                    ErrorMessage: $"Nao e possivel registrar chegada no status {appointment.Status}.");
            }

            var latitude = request.Latitude;
            var longitude = request.Longitude;
            var accuracyMeters = request.AccuracyMeters;
            var manualReason = string.IsNullOrWhiteSpace(request.ManualReason) ? null : request.ManualReason.Trim();

            var hasLatitude = latitude.HasValue;
            var hasLongitude = longitude.HasValue;
            var hasCoordinates = hasLatitude && hasLongitude;
            if (hasLatitude != hasLongitude)
            {
                return new ServiceAppointmentOperationResultDto(false, ErrorCode: "invalid_location", ErrorMessage: "Latitude e longitude devem ser informadas juntas.");
            }

            if (hasCoordinates)
            {
                if (latitude!.Value is < -90 or > 90 || longitude!.Value is < -180 or > 180)
                {
                    return new ServiceAppointmentOperationResultDto(false, ErrorCode: "invalid_location", ErrorMessage: "Coordenadas invalidas para check-in.");
                }

                if (accuracyMeters.HasValue && accuracyMeters.Value < 0)
                {
                    return new ServiceAppointmentOperationResultDto(false, ErrorCode: "invalid_location", ErrorMessage: "Precisao do GPS invalida.");
                }
            }
            else if (string.IsNullOrWhiteSpace(manualReason))
            {
                return new ServiceAppointmentOperationResultDto(
                    false,
                    ErrorCode: "invalid_reason",
                    ErrorMessage: "Informe o motivo do check-in manual quando o GPS nao estiver disponivel.");
            }

            var nowUtc = DateTime.UtcNow;
            var previousStatus = appointment.Status;
            var previousOperationalStatus = ResolveCurrentOperationalStatus(appointment);
            appointment.ArrivedAtUtc = nowUtc;
            appointment.ArrivedLatitude = hasCoordinates ? latitude : null;
            appointment.ArrivedLongitude = hasCoordinates ? longitude : null;
            appointment.ArrivedAccuracyMeters = hasCoordinates ? accuracyMeters : null;
            appointment.ArrivedManualReason = hasCoordinates ? null : manualReason;
            appointment.Status = ServiceAppointmentStatus.Arrived;
            appointment.OperationalStatus = ServiceAppointmentOperationalStatus.OnSite;
            appointment.OperationalStatusUpdatedAtUtc = nowUtc;
            appointment.OperationalStatusReason = hasCoordinates ? "Prestador chegou ao local." : manualReason;
            appointment.UpdatedAt = nowUtc;
            await _serviceAppointmentRepository.UpdateAsync(appointment);

            var arrivalHistoryReason = hasCoordinates
                ? "Prestador registrou chegada com GPS."
                : $"Prestador registrou chegada manualmente. Motivo: {manualReason}";
            var arrivalMetadata = hasCoordinates
                ? $"Latitude={latitude:0.######};Longitude={longitude:0.######};AccuracyMeters={accuracyMeters:0.##}"
                : $"ManualReason={manualReason}";

            await _serviceAppointmentRepository.AddHistoryAsync(new ServiceAppointmentHistory
            {
                ServiceAppointmentId = appointment.Id,
                PreviousStatus = previousStatus,
                NewStatus = ServiceAppointmentStatus.Arrived,
                PreviousOperationalStatus = previousOperationalStatus,
                NewOperationalStatus = ServiceAppointmentOperationalStatus.OnSite,
                ActorUserId = actorUserId,
                ActorRole = ResolveActorRole(actorRole),
                Reason = arrivalHistoryReason,
                Metadata = arrivalMetadata
            });

            await _notificationService.SendNotificationAsync(
                appointment.ClientId.ToString("N"),
                "Agendamento: prestador chegou",
                "O prestador registrou chegada no local do servico.",
                BuildActionUrl(appointment.ServiceRequestId));

            await _notificationService.SendNotificationAsync(
                appointment.ProviderId.ToString("N"),
                "Agendamento: chegada registrada",
                "Seu check-in de chegada foi registrado com sucesso.",
                BuildActionUrl(appointment.ServiceRequestId));

            var loaded = await _serviceAppointmentRepository.GetByIdAsync(appointment.Id) ?? appointment;
            return new ServiceAppointmentOperationResultDto(true, MapToDto(loaded));
        }
        finally
        {
            operationLock.Release();
        }
    }

    public async Task<ServiceAppointmentOperationResultDto> StartExecutionAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        StartServiceAppointmentExecutionRequestDto request)
    {
        if (!IsProviderRole(actorRole) && !IsAdminRole(actorRole))
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Perfil sem permissao para iniciar atendimento.");
        }

        var operationLock = await AcquireAppointmentOperationalLockAsync(appointmentId);
        try
        {
            var appointment = await _serviceAppointmentRepository.GetByIdAsync(appointmentId);
            if (appointment == null)
            {
                return new ServiceAppointmentOperationResultDto(false, ErrorCode: "appointment_not_found", ErrorMessage: "Agendamento nao encontrado.");
            }

            if (!IsAdminRole(actorRole) && appointment.ProviderId != actorUserId)
            {
                return new ServiceAppointmentOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Prestador nao pode iniciar atendimento de outro prestador.");
            }

            if (appointment.StartedAtUtc.HasValue)
            {
                return new ServiceAppointmentOperationResultDto(false, ErrorCode: "duplicate_start", ErrorMessage: "Atendimento ja iniciado para este agendamento.");
            }

            if (!appointment.ArrivedAtUtc.HasValue)
            {
                return new ServiceAppointmentOperationResultDto(
                    false,
                    ErrorCode: "policy_violation",
                    ErrorMessage: "Registre a chegada antes de iniciar o atendimento.");
            }

            if (appointment.Status != ServiceAppointmentStatus.Arrived)
            {
                return new ServiceAppointmentOperationResultDto(
                    false,
                    ErrorCode: "invalid_state",
                    ErrorMessage: $"Nao e possivel iniciar atendimento no status {appointment.Status}.");
            }

            var nowUtc = DateTime.UtcNow;
            var previousStatus = appointment.Status;
            var previousOperationalStatus = ResolveCurrentOperationalStatus(appointment);
            var startReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

            appointment.Status = ServiceAppointmentStatus.InProgress;
            appointment.StartedAtUtc = nowUtc;
            appointment.OperationalStatus = ServiceAppointmentOperationalStatus.InService;
            appointment.OperationalStatusUpdatedAtUtc = nowUtc;
            appointment.OperationalStatusReason = string.IsNullOrWhiteSpace(startReason)
                ? "Prestador iniciou o atendimento."
                : startReason;
            appointment.UpdatedAt = nowUtc;
            await _serviceAppointmentRepository.UpdateAsync(appointment);

            await _serviceAppointmentRepository.AddHistoryAsync(new ServiceAppointmentHistory
            {
                ServiceAppointmentId = appointment.Id,
                PreviousStatus = previousStatus,
                NewStatus = ServiceAppointmentStatus.InProgress,
                PreviousOperationalStatus = previousOperationalStatus,
                NewOperationalStatus = ServiceAppointmentOperationalStatus.InService,
                ActorUserId = actorUserId,
                ActorRole = ResolveActorRole(actorRole),
                Reason = string.IsNullOrWhiteSpace(startReason) ? "Prestador iniciou o atendimento." : startReason
            });

            if (appointment.ServiceRequest is { } serviceRequest &&
                !IsServiceRequestClosedForScheduling(serviceRequest.Status) &&
                serviceRequest.Status != ServiceRequestStatus.InProgress)
            {
                serviceRequest.Status = ServiceRequestStatus.InProgress;
                await _serviceRequestRepository.UpdateAsync(serviceRequest);
            }

            await _notificationService.SendNotificationAsync(
                appointment.ClientId.ToString("N"),
                "Agendamento: atendimento iniciado",
                "O prestador iniciou o atendimento do seu pedido.",
                BuildActionUrl(appointment.ServiceRequestId));

            await _notificationService.SendNotificationAsync(
                appointment.ProviderId.ToString("N"),
                "Agendamento: atendimento iniciado",
                "Voce iniciou o atendimento deste agendamento.",
                BuildActionUrl(appointment.ServiceRequestId));

            var loaded = await _serviceAppointmentRepository.GetByIdAsync(appointment.Id) ?? appointment;
            return new ServiceAppointmentOperationResultDto(true, MapToDto(loaded));
        }
        finally
        {
            operationLock.Release();
        }
    }

    public async Task<ServiceAppointmentOperationResultDto> RespondPresenceAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        RespondServiceAppointmentPresenceRequestDto request)
    {
        if (!IsClientRole(actorRole) && !IsProviderRole(actorRole))
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Perfil sem permissao para responder presenca.");
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (!string.IsNullOrWhiteSpace(reason) && reason.Length > 500)
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "invalid_reason", ErrorMessage: "Motivo deve ter no maximo 500 caracteres.");
        }

        var appointment = await _serviceAppointmentRepository.GetByIdAsync(appointmentId);
        if (appointment == null)
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "appointment_not_found", ErrorMessage: "Agendamento nao encontrado.");
        }

        if (!CanAccessAppointment(appointment, actorUserId, actorRole))
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Usuario sem acesso a este agendamento.");
        }

        if (!CanRespondPresenceForStatus(appointment.Status))
        {
            return new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "invalid_state",
                ErrorMessage: $"Nao e possivel responder presenca no status {appointment.Status}.");
        }

        var nowUtc = DateTime.UtcNow;
        var isClient = IsClientRole(actorRole);
        if (isClient)
        {
            appointment.ClientPresenceConfirmed = request.Confirmed;
            appointment.ClientPresenceRespondedAtUtc = nowUtc;
            appointment.ClientPresenceReason = reason;
        }
        else
        {
            appointment.ProviderPresenceConfirmed = request.Confirmed;
            appointment.ProviderPresenceRespondedAtUtc = nowUtc;
            appointment.ProviderPresenceReason = reason;
        }

        appointment.UpdatedAt = nowUtc;
        await _serviceAppointmentRepository.UpdateAsync(appointment);

        await _serviceAppointmentRepository.AddHistoryAsync(new ServiceAppointmentHistory
        {
            ServiceAppointmentId = appointment.Id,
            PreviousStatus = appointment.Status,
            NewStatus = appointment.Status,
            PreviousOperationalStatus = appointment.OperationalStatus,
            NewOperationalStatus = appointment.OperationalStatus,
            ActorUserId = actorUserId,
            ActorRole = ResolveActorRole(actorRole),
            Reason = request.Confirmed
                ? "Presenca confirmada."
                : "Presenca nao confirmada.",
            Metadata = JsonSerializer.Serialize(new
            {
                type = "presence_response",
                participant = isClient ? "client" : "provider",
                confirmed = request.Confirmed,
                reason
            })
        });

        await _appointmentReminderService.RegisterPresenceResponseTelemetryAsync(
            appointment.Id,
            actorUserId,
            request.Confirmed,
            reason,
            nowUtc);

        var counterpartUserId = isClient ? appointment.ProviderId : appointment.ClientId;
        var actorLabel = isClient ? "Cliente" : "Prestador";
        var presenceLabel = request.Confirmed ? "confirmou" : "nao confirmou";
        var message = string.IsNullOrWhiteSpace(reason)
            ? $"{actorLabel} {presenceLabel} presenca para o agendamento."
            : $"{actorLabel} {presenceLabel} presenca para o agendamento. Motivo: {reason}";

        await _notificationService.SendNotificationAsync(
            counterpartUserId.ToString("N"),
            "Agendamento: resposta de presenca",
            message,
            BuildActionUrl(appointment.ServiceRequestId));

        await _notificationService.SendNotificationAsync(
            actorUserId.ToString("N"),
            "Agendamento: resposta registrada",
            request.Confirmed
                ? "Sua confirmacao de presenca foi registrada."
                : "Sua resposta de nao confirmacao foi registrada.",
            BuildActionUrl(appointment.ServiceRequestId));

        var loaded = await _serviceAppointmentRepository.GetByIdAsync(appointment.Id) ?? appointment;
        return new ServiceAppointmentOperationResultDto(true, MapToDto(loaded));
    }

    public async Task<ServiceAppointmentOperationResultDto> UpdateOperationalStatusAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        UpdateServiceAppointmentOperationalStatusRequestDto request)
    {
        if (!IsProviderRole(actorRole) && !IsAdminRole(actorRole))
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Perfil sem permissao para atualizar status operacional.");
        }

        if (!ServiceAppointmentOperationalStatusExtensions.TryParseFlexible(request.Status, out var targetStatus))
        {
            return new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "invalid_operational_status",
                ErrorMessage: "Status operacional invalido.");
        }

        var normalizedReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (targetStatus == ServiceAppointmentOperationalStatus.WaitingParts && string.IsNullOrWhiteSpace(normalizedReason))
        {
            return new ServiceAppointmentOperationResultDto(
                false,
                ErrorCode: "invalid_reason",
                ErrorMessage: "Informe o motivo ao marcar atendimento como aguardando peca.");
        }

        var operationLock = await AcquireAppointmentOperationalLockAsync(appointmentId);
        SemaphoreSlim? requestLock = null;
        try
        {
            var appointment = await _serviceAppointmentRepository.GetByIdAsync(appointmentId);
            if (appointment == null)
            {
                return new ServiceAppointmentOperationResultDto(false, ErrorCode: "appointment_not_found", ErrorMessage: "Agendamento nao encontrado.");
            }

            if (!IsAdminRole(actorRole) && appointment.ProviderId != actorUserId)
            {
                return new ServiceAppointmentOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Prestador nao pode atualizar status operacional de outro prestador.");
            }

            if (appointment.Status is ServiceAppointmentStatus.CancelledByClient or
                ServiceAppointmentStatus.CancelledByProvider or
                ServiceAppointmentStatus.RejectedByProvider or
                ServiceAppointmentStatus.ExpiredWithoutProviderAction)
            {
                return new ServiceAppointmentOperationResultDto(
                    false,
                    ErrorCode: "invalid_state",
                    ErrorMessage: $"Nao e possivel alterar status operacional no status {appointment.Status}.");
            }

            var nowUtc = DateTime.UtcNow;
            var previousStatus = appointment.Status;
            var previousOperationalStatus = ResolveCurrentOperationalStatus(appointment);
            if (previousOperationalStatus == targetStatus)
            {
                return new ServiceAppointmentOperationResultDto(true, MapToDto(appointment));
            }

            if (!CanTransitionOperationalStatus(previousOperationalStatus, targetStatus))
            {
                var previousLabel = previousOperationalStatus?.ToPtBr() ?? "sem status";
                return new ServiceAppointmentOperationResultDto(
                    false,
                    ErrorCode: "invalid_operational_transition",
                    ErrorMessage: $"Transicao invalida: {previousLabel} -> {targetStatus.ToPtBr()}.");
            }

            if (targetStatus == ServiceAppointmentOperationalStatus.Completed)
            {
                requestLock = await AcquireServiceRequestScopeChangeLockAsync(appointment.ServiceRequestId);

                var checklistValidation = await _serviceAppointmentChecklistService.ValidateRequiredItemsForCompletionAsync(
                    appointment.Id,
                    actorRole);

                if (!checklistValidation.Success)
                {
                    return new ServiceAppointmentOperationResultDto(
                        false,
                        ErrorCode: checklistValidation.ErrorCode ?? "checklist_validation_failed",
                        ErrorMessage: checklistValidation.ErrorMessage ?? "Nao foi possivel validar checklist tecnico.");
                }

                if (!checklistValidation.CanComplete)
                {
                    return new ServiceAppointmentOperationResultDto(
                        false,
                        ErrorCode: "required_checklist_pending",
                        ErrorMessage: checklistValidation.ErrorMessage ?? "Checklist obrigatorio incompleto para concluir o atendimento.");
                }

                var pendingScopeChange = await _scopeChangeRequestRepository.GetLatestByAppointmentIdAndStatusAsync(
                    appointment.Id,
                    ServiceScopeChangeRequestStatus.PendingClientApproval);
                if (pendingScopeChange != null)
                {
                    if (IsScopeChangeRequestClientApprovalTimedOut(pendingScopeChange, nowUtc))
                    {
                        await ExpireScopeChangeRequestByTimeoutAsync(
                            appointment,
                            pendingScopeChange,
                            nowUtc,
                            "Expiracao automatica ao validar conclusao operacional.");
                    }
                    else
                    {
                        return new ServiceAppointmentOperationResultDto(
                            false,
                            ErrorCode: "scope_change_pending",
                            ErrorMessage: "Existe aditivo pendente para este agendamento. Responda o aditivo antes de concluir.");
                    }
                }
            }

            switch (targetStatus)
            {
                case ServiceAppointmentOperationalStatus.OnSite:
                    appointment.Status = ServiceAppointmentStatus.Arrived;
                    appointment.ArrivedAtUtc ??= nowUtc;
                    appointment.ArrivedManualReason ??= "Atualizado via status operacional.";
                    break;

                case ServiceAppointmentOperationalStatus.InService:
                    appointment.Status = ServiceAppointmentStatus.InProgress;
                    appointment.ArrivedAtUtc ??= nowUtc;
                    appointment.ArrivedManualReason ??= "Atualizado via status operacional.";
                    appointment.StartedAtUtc ??= nowUtc;
                    break;

                case ServiceAppointmentOperationalStatus.WaitingParts:
                    appointment.Status = ServiceAppointmentStatus.InProgress;
                    appointment.ArrivedAtUtc ??= nowUtc;
                    appointment.StartedAtUtc ??= nowUtc;
                    break;

                case ServiceAppointmentOperationalStatus.Completed:
                    appointment.Status = ServiceAppointmentStatus.Completed;
                    appointment.ArrivedAtUtc ??= nowUtc;
                    appointment.StartedAtUtc ??= nowUtc;
                    appointment.CompletedAtUtc ??= nowUtc;
                    break;

                case ServiceAppointmentOperationalStatus.OnTheWay:
                    if (appointment.Status is not ServiceAppointmentStatus.Confirmed and not ServiceAppointmentStatus.RescheduleConfirmed)
                    {
                        appointment.Status = ServiceAppointmentStatus.Confirmed;
                    }
                    break;
            }

            appointment.OperationalStatus = targetStatus;
            appointment.OperationalStatusUpdatedAtUtc = nowUtc;
            appointment.OperationalStatusReason = normalizedReason;
            appointment.UpdatedAt = nowUtc;
            await _serviceAppointmentRepository.UpdateAsync(appointment);

            await _serviceAppointmentRepository.AddHistoryAsync(new ServiceAppointmentHistory
            {
                ServiceAppointmentId = appointment.Id,
                PreviousStatus = previousStatus,
                NewStatus = appointment.Status,
                PreviousOperationalStatus = previousOperationalStatus,
                NewOperationalStatus = targetStatus,
                ActorUserId = actorUserId,
                ActorRole = ResolveActorRole(actorRole),
                Reason = string.IsNullOrWhiteSpace(normalizedReason)
                    ? $"Status operacional atualizado para {targetStatus.ToPtBr()}."
                    : normalizedReason,
                Metadata = $"Transition=OperationalStatus;Previous={previousOperationalStatus};Current={targetStatus}"
            });

            if (appointment.ServiceRequest is { } serviceRequest &&
                !IsServiceRequestClosedForScheduling(serviceRequest.Status))
            {
                if (targetStatus == ServiceAppointmentOperationalStatus.InService &&
                    serviceRequest.Status != ServiceRequestStatus.InProgress)
                {
                    serviceRequest.Status = ServiceRequestStatus.InProgress;
                    await _serviceRequestRepository.UpdateAsync(serviceRequest);
                }
                else if (targetStatus == ServiceAppointmentOperationalStatus.Completed &&
                         serviceRequest.Status != ServiceRequestStatus.PendingClientCompletionAcceptance)
                {
                    serviceRequest.Status = ServiceRequestStatus.PendingClientCompletionAcceptance;
                    await _serviceRequestRepository.UpdateAsync(serviceRequest);
                }
            }

            if (targetStatus == ServiceAppointmentOperationalStatus.Completed)
            {
                var completionPinResult = await UpsertCompletionTermPinAsync(
                    appointment,
                    forceRegenerate: true,
                    normalizedReason,
                    nowUtc);

                if (completionPinResult.Success && !string.IsNullOrWhiteSpace(completionPinResult.OneTimePin))
                {
                    await _notificationService.SendNotificationAsync(
                        appointment.ClientId.ToString("N"),
                        "Agendamento: PIN de aceite de conclusao",
                        $"PIN de aceite para conclusao: {completionPinResult.OneTimePin}. Expira em {_completionPinExpiryMinutes} minuto(s).",
                        BuildActionUrl(appointment.ServiceRequestId));
                }
            }

            var actorPrefix = IsAdminRole(actorRole) ? "Administrador" : "Prestador";
            var targetLabel = targetStatus.ToPtBr();
            var reasonSuffix = string.IsNullOrWhiteSpace(normalizedReason) ? string.Empty : $" Motivo: {normalizedReason}";
            var notificationMessage = $"{actorPrefix} atualizou o status do agendamento para {targetLabel}.{reasonSuffix}";

            await _notificationService.SendNotificationAsync(
                appointment.ClientId.ToString("N"),
                "Agendamento: status operacional atualizado",
                notificationMessage,
                BuildActionUrl(appointment.ServiceRequestId));

            await _notificationService.SendNotificationAsync(
                appointment.ProviderId.ToString("N"),
                "Agendamento: status operacional atualizado",
                notificationMessage,
                BuildActionUrl(appointment.ServiceRequestId));

            var loaded = await _serviceAppointmentRepository.GetByIdAsync(appointment.Id) ?? appointment;
            return new ServiceAppointmentOperationResultDto(true, MapToDto(loaded));
        }
        finally
        {
            requestLock?.Release();
            operationLock.Release();
        }
    }

    public async Task<ServiceScopeChangeRequestOperationResultDto> CreateScopeChangeRequestAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        CreateServiceScopeChangeRequestDto request)
    {
        if (!IsProviderRole(actorRole) && !IsAdminRole(actorRole))
        {
            return new ServiceScopeChangeRequestOperationResultDto(
                false,
                ErrorCode: "forbidden",
                ErrorMessage: "Perfil sem permissao para solicitar aditivo.");
        }

        if (appointmentId == Guid.Empty)
        {
            return new ServiceScopeChangeRequestOperationResultDto(
                false,
                ErrorCode: "invalid_appointment",
                ErrorMessage: "Agendamento invalido.");
        }

        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new ServiceScopeChangeRequestOperationResultDto(
                false,
                ErrorCode: "invalid_scope_change_reason",
                ErrorMessage: "Motivo do aditivo e obrigatorio.");
        }

        var additionalScopeDescription = request.AdditionalScopeDescription?.Trim();
        if (string.IsNullOrWhiteSpace(additionalScopeDescription))
        {
            return new ServiceScopeChangeRequestOperationResultDto(
                false,
                ErrorCode: "invalid_scope_change_description",
                ErrorMessage: "Descricao do escopo adicional e obrigatoria.");
        }

        var incrementalValue = decimal.Round(request.IncrementalValue, 2, MidpointRounding.AwayFromZero);
        if (incrementalValue <= 0m)
        {
            return new ServiceScopeChangeRequestOperationResultDto(
                false,
                ErrorCode: "invalid_scope_change_value",
                ErrorMessage: "Valor incremental deve ser maior que zero.");
        }

        var operationLock = await AcquireAppointmentOperationalLockAsync(appointmentId);
        SemaphoreSlim? requestLock = null;
        try
        {
            var appointment = await _serviceAppointmentRepository.GetByIdAsync(appointmentId);
            if (appointment == null)
            {
                return new ServiceScopeChangeRequestOperationResultDto(
                    false,
                    ErrorCode: "appointment_not_found",
                    ErrorMessage: "Agendamento nao encontrado.");
            }

            if (!IsAdminRole(actorRole) && appointment.ProviderId != actorUserId)
            {
                return new ServiceScopeChangeRequestOperationResultDto(
                    false,
                    ErrorCode: "forbidden",
                    ErrorMessage: "Prestador nao pode solicitar aditivo para agendamento de outro prestador.");
            }

            requestLock = await AcquireServiceRequestScopeChangeLockAsync(appointment.ServiceRequestId);

            if (!IsScopeChangeCreationAllowedStatus(appointment.Status))
            {
                return new ServiceScopeChangeRequestOperationResultDto(
                    false,
                    ErrorCode: "invalid_state",
                    ErrorMessage: $"Nao e possivel solicitar aditivo no status {appointment.Status}.");
            }

            var serviceRequest = await _serviceRequestRepository.GetByIdAsync(appointment.ServiceRequestId)
                ?? appointment.ServiceRequest;
            if (serviceRequest == null)
            {
                return new ServiceScopeChangeRequestOperationResultDto(
                    false,
                    ErrorCode: "request_not_found",
                    ErrorMessage: "Pedido de servico nao encontrado.");
            }

            if (IsServiceRequestClosedForScheduling(serviceRequest.Status))
            {
                return new ServiceScopeChangeRequestOperationResultDto(
                    false,
                    ErrorCode: "invalid_state",
                    ErrorMessage: "Pedido encerrado nao permite solicitacao de aditivo.");
            }

            var provider = await _userRepository.GetByIdAsync(appointment.ProviderId);
            var providerPlan = provider?.ProviderProfile?.Plan ?? ProviderPlan.Trial;
            var scopePolicy = ResolveScopeChangePolicy(providerPlan);
            var acceptedProposalValue = serviceRequest.Proposals
                .Where(p => p.ProviderId == appointment.ProviderId && p.Accepted && !p.IsInvalidated)
                .Select(p => p.EstimatedValue)
                .Where(v => v.HasValue && v.Value > 0m)
                .Select(v => v!.Value)
                .DefaultIfEmpty(0m)
                .Max();

            var maxAllowedValue = ResolveScopeChangeLimit(scopePolicy, acceptedProposalValue);
            if (incrementalValue > maxAllowedValue)
            {
                var formattedAllowed = maxAllowedValue.ToString("C2", new CultureInfo("pt-BR"));
                return new ServiceScopeChangeRequestOperationResultDto(
                    false,
                    ErrorCode: "policy_violation",
                    ErrorMessage: $"Aditivo acima do limite para o plano {providerPlan.ToPtBr()}. Valor maximo permitido: {formattedAllowed}.");
            }

            var nowUtc = DateTime.UtcNow;
            var pendingRequest = await _scopeChangeRequestRepository.GetLatestByAppointmentIdAndStatusAsync(
                appointment.Id,
                ServiceScopeChangeRequestStatus.PendingClientApproval);
            if (pendingRequest != null)
            {
                if (IsScopeChangeRequestClientApprovalTimedOut(pendingRequest, nowUtc))
                {
                    await ExpireScopeChangeRequestByTimeoutAsync(
                        appointment,
                        pendingRequest,
                        nowUtc,
                        "Expiracao automatica ao avaliar nova solicitacao de aditivo.");
                }
                else
                {
                    return new ServiceScopeChangeRequestOperationResultDto(
                        false,
                        ErrorCode: "scope_change_pending",
                        ErrorMessage: "Ja existe um aditivo pendente para este agendamento.");
                }
            }

            var latestVersion = await _scopeChangeRequestRepository.GetLatestByAppointmentIdAsync(appointment.Id);
            var scopeChangeRequest = new ServiceScopeChangeRequest
            {
                ServiceRequestId = appointment.ServiceRequestId,
                ServiceAppointmentId = appointment.Id,
                ProviderId = appointment.ProviderId,
                Version = (latestVersion?.Version ?? 0) + 1,
                Status = ServiceScopeChangeRequestStatus.PendingClientApproval,
                Reason = reason,
                AdditionalScopeDescription = additionalScopeDescription,
                IncrementalValue = incrementalValue,
                RequestedAtUtc = nowUtc,
                PreviousVersionId = latestVersion?.Id
            };

            await _scopeChangeRequestRepository.AddAsync(scopeChangeRequest);
            await AppendScopeChangeAuditHistoryAsync(
                appointment,
                actorUserId,
                actorRole,
                scopeChangeRequest,
                "created");
            var commercialTotals = await _serviceRequestCommercialValueService.RecalculateAsync(serviceRequest);
            serviceRequest.CommercialVersion = Math.Max(1, serviceRequest.CommercialVersion);
            serviceRequest.CommercialBaseValue = commercialTotals.BaseValue;
            serviceRequest.CommercialCurrentValue = commercialTotals.CurrentValue;
            serviceRequest.CommercialState = ServiceRequestCommercialState.PendingClientApproval;
            serviceRequest.CommercialUpdatedAtUtc = nowUtc;
            await _serviceRequestRepository.UpdateAsync(serviceRequest);

            var formattedValue = incrementalValue.ToString("C2", new CultureInfo("pt-BR"));
            var actionUrl = $"{BuildActionUrl(appointment.ServiceRequestId)}?scopeChangeId={scopeChangeRequest.Id}";
            await _notificationService.SendNotificationAsync(
                appointment.ClientId.ToString("N"),
                "Solicitacao de aditivo",
                $"O prestador solicitou um aditivo de {formattedValue}. Acesse o pedido para aprovar ou rejeitar.",
                actionUrl);

            await _notificationService.SendNotificationAsync(
                appointment.ProviderId.ToString("N"),
                "Aditivo enviado",
                $"Solicitacao de aditivo v{scopeChangeRequest.Version} enviada com sucesso.",
                actionUrl);

            return new ServiceScopeChangeRequestOperationResultDto(
                true,
                ScopeChangeRequest: MapScopeChangeRequestToDto(scopeChangeRequest));
        }
        finally
        {
            requestLock?.Release();
            operationLock.Release();
        }
    }

    public async Task<ServiceScopeChangeAttachmentOperationResultDto> AddScopeChangeAttachmentAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        Guid scopeChangeRequestId,
        RegisterServiceScopeChangeAttachmentDto request)
    {
        if (!IsProviderRole(actorRole) && !IsAdminRole(actorRole))
        {
            return new ServiceScopeChangeAttachmentOperationResultDto(
                false,
                ErrorCode: "forbidden",
                ErrorMessage: "Perfil sem permissao para anexar evidencia.");
        }

        if (appointmentId == Guid.Empty || scopeChangeRequestId == Guid.Empty)
        {
            return new ServiceScopeChangeAttachmentOperationResultDto(
                false,
                ErrorCode: "invalid_scope_change",
                ErrorMessage: "Solicitacao de aditivo invalida.");
        }

        var fileUrl = request.FileUrl?.Trim();
        var fileName = request.FileName?.Trim();
        var contentType = request.ContentType?.Trim();
        if (string.IsNullOrWhiteSpace(fileUrl) ||
            string.IsNullOrWhiteSpace(fileName) ||
            string.IsNullOrWhiteSpace(contentType))
        {
            return new ServiceScopeChangeAttachmentOperationResultDto(
                false,
                ErrorCode: "invalid_attachment",
                ErrorMessage: "Metadados do anexo invalidos.");
        }

        if (request.SizeBytes <= 0 || request.SizeBytes > 25_000_000)
        {
            return new ServiceScopeChangeAttachmentOperationResultDto(
                false,
                ErrorCode: "invalid_attachment_size",
                ErrorMessage: "Anexo excede o limite permitido.");
        }

        var operationLock = await AcquireAppointmentOperationalLockAsync(appointmentId);
        try
        {
            var appointment = await _serviceAppointmentRepository.GetByIdAsync(appointmentId);
            if (appointment == null)
            {
                return new ServiceScopeChangeAttachmentOperationResultDto(
                    false,
                    ErrorCode: "appointment_not_found",
                    ErrorMessage: "Agendamento nao encontrado.");
            }

            var scopeChange = await _scopeChangeRequestRepository.GetByIdWithAttachmentsAsync(scopeChangeRequestId);
            if (scopeChange == null || scopeChange.ServiceAppointmentId != appointmentId)
            {
                return new ServiceScopeChangeAttachmentOperationResultDto(
                    false,
                    ErrorCode: "scope_change_not_found",
                    ErrorMessage: "Solicitacao de aditivo nao encontrada.");
            }

            if (!IsAdminRole(actorRole) && scopeChange.ProviderId != actorUserId)
            {
                return new ServiceScopeChangeAttachmentOperationResultDto(
                    false,
                    ErrorCode: "forbidden",
                    ErrorMessage: "Prestador sem permissao para anexar evidencia neste aditivo.");
            }

            if (scopeChange.Status != ServiceScopeChangeRequestStatus.PendingClientApproval)
            {
                return new ServiceScopeChangeAttachmentOperationResultDto(
                    false,
                    ErrorCode: "invalid_state",
                    ErrorMessage: "Nao e possivel anexar evidencia para um aditivo que nao esta pendente.");
            }

            if ((scopeChange.Attachments?.Count ?? 0) >= 10)
            {
                return new ServiceScopeChangeAttachmentOperationResultDto(
                    false,
                    ErrorCode: "attachment_limit_exceeded",
                    ErrorMessage: "Limite de 10 anexos por aditivo atingido.");
            }

            var mediaKind = ResolveScopeAttachmentMediaKind(contentType, fileName);
            var attachment = new ServiceScopeChangeRequestAttachment
            {
                ServiceScopeChangeRequestId = scopeChange.Id,
                UploadedByUserId = actorUserId,
                FileUrl = fileUrl,
                FileName = fileName,
                ContentType = contentType,
                MediaKind = mediaKind,
                SizeBytes = request.SizeBytes
            };

            await _scopeChangeRequestRepository.AddAttachmentAsync(attachment);
            await AppendScopeChangeAuditHistoryAsync(
                appointment,
                actorUserId,
                actorRole,
                scopeChange,
                "attachment_added",
                reason: attachment.FileName);

            return new ServiceScopeChangeAttachmentOperationResultDto(
                true,
                Attachment: MapScopeChangeAttachmentToDto(attachment));
        }
        finally
        {
            operationLock.Release();
        }
    }

    public Task<ServiceScopeChangeRequestOperationResultDto> ApproveScopeChangeRequestAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        Guid scopeChangeRequestId)
    {
        return RespondScopeChangeRequestAsync(
            actorUserId,
            actorRole,
            appointmentId,
            scopeChangeRequestId,
            approve: true,
            reason: null);
    }

    public Task<ServiceScopeChangeRequestOperationResultDto> RejectScopeChangeRequestAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        Guid scopeChangeRequestId,
        RejectServiceScopeChangeRequestDto request)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Task.FromResult(new ServiceScopeChangeRequestOperationResultDto(
                false,
                ErrorCode: "invalid_reason",
                ErrorMessage: "Motivo da rejeicao e obrigatorio."));
        }

        return RespondScopeChangeRequestAsync(
            actorUserId,
            actorRole,
            appointmentId,
            scopeChangeRequestId,
            approve: false,
            reason);
    }

    public async Task<IReadOnlyList<ServiceScopeChangeRequestDto>> GetScopeChangeRequestsByServiceRequestAsync(
        Guid actorUserId,
        string actorRole,
        Guid serviceRequestId)
    {
        if (serviceRequestId == Guid.Empty)
        {
            return Array.Empty<ServiceScopeChangeRequestDto>();
        }

        if (!IsAdminRole(actorRole) && !IsClientRole(actorRole) && !IsProviderRole(actorRole))
        {
            return Array.Empty<ServiceScopeChangeRequestDto>();
        }

        var appointments = await _serviceAppointmentRepository.GetByRequestIdAsync(serviceRequestId);
        if (appointments.Count == 0)
        {
            return Array.Empty<ServiceScopeChangeRequestDto>();
        }

        var accessibleAppointments = appointments
            .Where(appointment => CanAccessAppointment(appointment, actorUserId, actorRole))
            .ToList();
        if (accessibleAppointments.Count == 0)
        {
            return Array.Empty<ServiceScopeChangeRequestDto>();
        }

        var allowedAppointmentIds = accessibleAppointments
            .Select(appointment => appointment.Id)
            .ToHashSet();

        var scopeChanges = await _scopeChangeRequestRepository.GetByServiceRequestIdAsync(serviceRequestId);
        if (scopeChanges.Count == 0)
        {
            return Array.Empty<ServiceScopeChangeRequestDto>();
        }

        return scopeChanges
            .Where(scopeChange => allowedAppointmentIds.Contains(scopeChange.ServiceAppointmentId))
            .OrderByDescending(scopeChange => scopeChange.RequestedAtUtc)
            .ThenByDescending(scopeChange => scopeChange.Version)
            .Select(MapScopeChangeRequestToDto)
            .ToList();
    }

    private async Task<ServiceScopeChangeRequestOperationResultDto> RespondScopeChangeRequestAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        Guid scopeChangeRequestId,
        bool approve,
        string? reason)
    {
        if (!IsClientRole(actorRole) && !IsAdminRole(actorRole))
        {
            return new ServiceScopeChangeRequestOperationResultDto(
                false,
                ErrorCode: "forbidden",
                ErrorMessage: "Perfil sem permissao para responder aditivo.");
        }

        if (appointmentId == Guid.Empty || scopeChangeRequestId == Guid.Empty)
        {
            return new ServiceScopeChangeRequestOperationResultDto(
                false,
                ErrorCode: "invalid_scope_change",
                ErrorMessage: "Solicitacao de aditivo invalida.");
        }

        var operationLock = await AcquireAppointmentOperationalLockAsync(appointmentId);
        SemaphoreSlim? requestLock = null;
        try
        {
            var appointment = await _serviceAppointmentRepository.GetByIdAsync(appointmentId);
            if (appointment == null)
            {
                return new ServiceScopeChangeRequestOperationResultDto(
                    false,
                    ErrorCode: "appointment_not_found",
                    ErrorMessage: "Agendamento nao encontrado.");
            }

            if (!IsAdminRole(actorRole) && appointment.ClientId != actorUserId)
            {
                return new ServiceScopeChangeRequestOperationResultDto(
                    false,
                    ErrorCode: "forbidden",
                    ErrorMessage: "Cliente sem permissao para responder este aditivo.");
            }

            requestLock = await AcquireServiceRequestScopeChangeLockAsync(appointment.ServiceRequestId);

            var scopeChangeRequest = await _scopeChangeRequestRepository.GetByIdWithAttachmentsAsync(scopeChangeRequestId);
            if (scopeChangeRequest == null || scopeChangeRequest.ServiceAppointmentId != appointmentId)
            {
                return new ServiceScopeChangeRequestOperationResultDto(
                    false,
                    ErrorCode: "scope_change_not_found",
                    ErrorMessage: "Solicitacao de aditivo nao encontrada.");
            }

            var nowUtc = DateTime.UtcNow;
            if (scopeChangeRequest.Status == ServiceScopeChangeRequestStatus.Expired)
            {
                return new ServiceScopeChangeRequestOperationResultDto(
                    false,
                    ErrorCode: "scope_change_expired",
                    ErrorMessage: "Prazo de resposta do aditivo expirou.");
            }

            if (scopeChangeRequest.Status == ServiceScopeChangeRequestStatus.PendingClientApproval &&
                IsScopeChangeRequestClientApprovalTimedOut(scopeChangeRequest, nowUtc))
            {
                await ExpireScopeChangeRequestByTimeoutAsync(
                    appointment,
                    scopeChangeRequest,
                    nowUtc,
                    "Expiracao automatica durante tentativa de resposta do cliente.");

                return new ServiceScopeChangeRequestOperationResultDto(
                    false,
                    ErrorCode: "scope_change_expired",
                    ErrorMessage: "Prazo de resposta do aditivo expirou.");
            }

            if (scopeChangeRequest.Status != ServiceScopeChangeRequestStatus.PendingClientApproval)
            {
                return new ServiceScopeChangeRequestOperationResultDto(
                    false,
                    ErrorCode: "invalid_state",
                    ErrorMessage: "Aditivo ja respondido anteriormente.");
            }

            scopeChangeRequest.Status = approve
                ? ServiceScopeChangeRequestStatus.ApprovedByClient
                : ServiceScopeChangeRequestStatus.RejectedByClient;
            scopeChangeRequest.ClientRespondedAtUtc = nowUtc;
            scopeChangeRequest.ClientResponseReason = approve ? null : reason;
            scopeChangeRequest.UpdatedAt = nowUtc;
            await _scopeChangeRequestRepository.UpdateAsync(scopeChangeRequest);
            await AppendScopeChangeAuditHistoryAsync(
                appointment,
                actorUserId,
                actorRole,
                scopeChangeRequest,
                approve ? "approved" : "rejected",
                reason);
            var serviceRequest = appointment.ServiceRequest
                ?? await _serviceRequestRepository.GetByIdAsync(scopeChangeRequest.ServiceRequestId);
            if (serviceRequest != null)
            {
                var commercialTotals = await _serviceRequestCommercialValueService.RecalculateAsync(serviceRequest);
                serviceRequest.CommercialVersion = Math.Max(1, serviceRequest.CommercialVersion);
                serviceRequest.CommercialBaseValue = commercialTotals.BaseValue;
                serviceRequest.CommercialCurrentValue = commercialTotals.CurrentValue;

                if (approve)
                {
                    serviceRequest.CommercialVersion++;
                }

                serviceRequest.CommercialState = ServiceRequestCommercialState.Stable;
                serviceRequest.CommercialUpdatedAtUtc = nowUtc;
                await _serviceRequestRepository.UpdateAsync(serviceRequest);
            }

            var actionUrl = $"{BuildActionUrl(scopeChangeRequest.ServiceRequestId)}?scopeChangeId={scopeChangeRequest.Id}";
            var valueCulture = new CultureInfo("pt-BR");
            var requestedIncrementalValue = decimal.Round(
                Math.Max(0m, scopeChangeRequest.IncrementalValue),
                2,
                MidpointRounding.AwayFromZero);
            var commercialCurrentValue = decimal.Round(
                Math.Max(0m, serviceRequest?.CommercialCurrentValue ?? 0m),
                2,
                MidpointRounding.AwayFromZero);
            var commercialPreviousValue = approve
                ? decimal.Round(
                    Math.Max(0m, commercialCurrentValue - requestedIncrementalValue),
                    2,
                    MidpointRounding.AwayFromZero)
                : commercialCurrentValue;
            var approvalSummary = $"Aditivo v{scopeChangeRequest.Version} aprovado. " +
                                  $"Valor anterior: {commercialPreviousValue.ToString("C2", valueCulture)}. " +
                                  $"Novo valor: {commercialCurrentValue.ToString("C2", valueCulture)}. " +
                                  $"Incremento: {requestedIncrementalValue.ToString("C2", valueCulture)}.";
            var rejectionSummary = $"Aditivo v{scopeChangeRequest.Version} rejeitado. " +
                                   $"Valor permanece em {commercialCurrentValue.ToString("C2", valueCulture)}. " +
                                   $"Incremento solicitado: {requestedIncrementalValue.ToString("C2", valueCulture)}.";
            if (approve)
            {
                await _notificationService.SendNotificationAsync(
                    appointment.ProviderId.ToString("N"),
                    "Aditivo aprovado pelo cliente",
                    approvalSummary,
                    actionUrl);

                await _notificationService.SendNotificationAsync(
                    appointment.ClientId.ToString("N"),
                    "Aditivo aprovado",
                    $"Voce aprovou o aditivo solicitado pelo prestador. {approvalSummary}",
                    actionUrl);
            }
            else
            {
                await _notificationService.SendNotificationAsync(
                    appointment.ProviderId.ToString("N"),
                    "Aditivo rejeitado pelo cliente",
                    $"{rejectionSummary} Motivo informado: {reason}",
                    actionUrl);

                await _notificationService.SendNotificationAsync(
                    appointment.ClientId.ToString("N"),
                    "Aditivo rejeitado",
                    $"{rejectionSummary} Voce rejeitou o aditivo solicitado pelo prestador.",
                    actionUrl);
            }

            return new ServiceScopeChangeRequestOperationResultDto(
                true,
                ScopeChangeRequest: MapScopeChangeRequestToDto(scopeChangeRequest));
        }
        finally
        {
            requestLock?.Release();
            operationLock.Release();
        }
    }

    public async Task<ServiceCompletionPinResultDto> GenerateCompletionPinAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        GenerateServiceCompletionPinRequestDto request)
    {
        if (!IsProviderRole(actorRole) && !IsAdminRole(actorRole))
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "forbidden",
                ErrorMessage: "Apenas prestador ou admin podem gerar PIN de aceite.");
        }

        if (appointmentId == Guid.Empty)
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "invalid_appointment",
                ErrorMessage: "Agendamento invalido.");
        }

        var appointment = await _serviceAppointmentRepository.GetByIdAsync(appointmentId);
        if (appointment == null)
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "appointment_not_found",
                ErrorMessage: "Agendamento nao encontrado.");
        }

        if (!IsAdminRole(actorRole) && appointment.ProviderId != actorUserId)
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "forbidden",
                ErrorMessage: "Prestador sem permissao para gerar PIN deste agendamento.");
        }

        if (appointment.Status != ServiceAppointmentStatus.Completed)
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "invalid_state",
                ErrorMessage: "PIN de aceite so pode ser gerado para agendamento concluido.");
        }

        var generated = await UpsertCompletionTermPinAsync(
            appointment,
            forceRegenerate: request.ForceRegenerate,
            request.Reason,
            DateTime.UtcNow);

        if (!generated.Success || generated.Term == null)
        {
            return generated;
        }

        if (!string.IsNullOrWhiteSpace(generated.OneTimePin))
        {
            var notificationText =
                $"PIN de aceite para conclusao: {generated.OneTimePin}. Expira em {_completionPinExpiryMinutes} minuto(s).";

            await _notificationService.SendNotificationAsync(
                appointment.ClientId.ToString("N"),
                "Agendamento: PIN de aceite de conclusao",
                notificationText,
                BuildActionUrl(appointment.ServiceRequestId));

            await _notificationService.SendNotificationAsync(
                appointment.ProviderId.ToString("N"),
                "Agendamento: PIN de aceite gerado",
                "Um novo PIN one-time de aceite foi gerado para o cliente.",
                BuildActionUrl(appointment.ServiceRequestId));
        }

        return generated;
    }

    public async Task<ServiceCompletionPinResultDto> ValidateCompletionPinAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        ValidateServiceCompletionPinRequestDto request)
    {
        if (!IsClientRole(actorRole) && !IsAdminRole(actorRole))
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "forbidden",
                ErrorMessage: "Apenas cliente ou admin podem validar o PIN de aceite.");
        }

        if (appointmentId == Guid.Empty)
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "invalid_appointment",
                ErrorMessage: "Agendamento invalido.");
        }

        var normalizedPin = NormalizePin(request.Pin);
        if (normalizedPin == null)
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "invalid_pin_format",
                ErrorMessage: $"PIN invalido. Informe um PIN numerico de {_completionPinLength} digitos.");
        }

        var appointment = await _serviceAppointmentRepository.GetByIdAsync(appointmentId);
        if (appointment == null)
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "appointment_not_found",
                ErrorMessage: "Agendamento nao encontrado.");
        }

        if (!IsAdminRole(actorRole) && appointment.ClientId != actorUserId)
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "forbidden",
                ErrorMessage: "Cliente sem permissao para validar PIN deste agendamento.");
        }

        var term = await _serviceCompletionTermRepository.GetByAppointmentIdAsync(appointmentId);
        if (term == null)
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "completion_term_not_found",
                ErrorMessage: "Termo de conclusao nao encontrado para este agendamento.");
        }

        if (term.Status != ServiceCompletionTermStatus.PendingClientAcceptance)
        {
            return new ServiceCompletionPinResultDto(
                false,
                Term: MapCompletionTermToDto(term),
                ErrorCode: "invalid_state",
                ErrorMessage: "Este termo nao esta mais aguardando aceite.");
        }

        var nowUtc = DateTime.UtcNow;
        if (!term.AcceptancePinExpiresAtUtc.HasValue || term.AcceptancePinExpiresAtUtc.Value <= nowUtc)
        {
            term.Status = ServiceCompletionTermStatus.Expired;
            term.AcceptancePinHashSha256 = null;
            term.AcceptancePinExpiresAtUtc = null;
            term.UpdatedAt = nowUtc;
            await _serviceCompletionTermRepository.UpdateAsync(term);

            return new ServiceCompletionPinResultDto(
                false,
                Term: MapCompletionTermToDto(term),
                ErrorCode: "pin_expired",
                ErrorMessage: "PIN expirado. Solicite um novo PIN ao prestador.");
        }

        if (string.IsNullOrWhiteSpace(term.AcceptancePinHashSha256))
        {
            return new ServiceCompletionPinResultDto(
                false,
                Term: MapCompletionTermToDto(term),
                ErrorCode: "invalid_state",
                ErrorMessage: "PIN de aceite indisponivel para este termo.");
        }

        var expectedHash = term.AcceptancePinHashSha256;
        var informedHash = BuildCompletionPinHash(appointment.Id, normalizedPin);
        if (!FixedTimeEqualsHex(expectedHash, informedHash))
        {
            term.AcceptancePinFailedAttempts++;
            term.UpdatedAt = nowUtc;

            if (term.AcceptancePinFailedAttempts >= _completionPinMaxFailedAttempts)
            {
                term.Status = ServiceCompletionTermStatus.EscalatedToAdmin;
                term.EscalatedAtUtc = nowUtc;
                term.AcceptancePinHashSha256 = null;
                term.AcceptancePinExpiresAtUtc = null;
            }

            await _serviceCompletionTermRepository.UpdateAsync(term);

            return new ServiceCompletionPinResultDto(
                false,
                Term: MapCompletionTermToDto(term),
                ErrorCode: term.Status == ServiceCompletionTermStatus.EscalatedToAdmin ? "pin_locked" : "invalid_pin",
                ErrorMessage: term.Status == ServiceCompletionTermStatus.EscalatedToAdmin
                    ? "Quantidade maxima de tentativas excedida. Caso encaminhado para analise."
                    : "PIN informado e invalido.");
        }

        await AcceptCompletionTermAsync(
            appointment,
            term,
            ServiceCompletionAcceptanceMethod.Pin,
            nowUtc,
            acceptedSignatureName: null);

        return new ServiceCompletionPinResultDto(true, MapCompletionTermToDto(term));
    }

    public async Task<ServiceCompletionPinResultDto> ConfirmCompletionAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        ConfirmServiceCompletionRequestDto request)
    {
        if (request == null)
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "invalid_input",
                ErrorMessage: "Dados de confirmacao nao informados.");
        }

        var method = request.Method?.Trim();
        if (string.IsNullOrWhiteSpace(method))
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "invalid_acceptance_method",
                ErrorMessage: "Informe o metodo de confirmacao (Pin ou SignatureName).");
        }

        if (string.Equals(method, ServiceCompletionAcceptanceMethod.Pin.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return await ValidateCompletionPinAsync(
                actorUserId,
                actorRole,
                appointmentId,
                new ValidateServiceCompletionPinRequestDto(request.Pin ?? string.Empty));
        }

        if (!string.Equals(method, ServiceCompletionAcceptanceMethod.SignatureName.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "invalid_acceptance_method",
                ErrorMessage: "Metodo de confirmacao invalido. Use Pin ou SignatureName.");
        }

        if (!IsClientRole(actorRole) && !IsAdminRole(actorRole))
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "forbidden",
                ErrorMessage: "Apenas cliente ou admin podem confirmar a conclusao.");
        }

        if (appointmentId == Guid.Empty)
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "invalid_appointment",
                ErrorMessage: "Agendamento invalido.");
        }

        var signatureName = request.SignatureName?.Trim();
        if (string.IsNullOrWhiteSpace(signatureName) || signatureName.Length < 3)
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "signature_required",
                ErrorMessage: "Informe o nome para assinatura com ao menos 3 caracteres.");
        }

        var appointment = await _serviceAppointmentRepository.GetByIdAsync(appointmentId);
        if (appointment == null)
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "appointment_not_found",
                ErrorMessage: "Agendamento nao encontrado.");
        }

        if (!IsAdminRole(actorRole) && appointment.ClientId != actorUserId)
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "forbidden",
                ErrorMessage: "Cliente sem permissao para confirmar este agendamento.");
        }

        var term = await _serviceCompletionTermRepository.GetByAppointmentIdAsync(appointmentId);
        if (term == null)
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "completion_term_not_found",
                ErrorMessage: "Termo de conclusao nao encontrado para este agendamento.");
        }

        if (term.Status != ServiceCompletionTermStatus.PendingClientAcceptance)
        {
            return new ServiceCompletionPinResultDto(
                false,
                Term: MapCompletionTermToDto(term),
                ErrorCode: "invalid_state",
                ErrorMessage: "Este termo nao esta mais aguardando aceite.");
        }

        await AcceptCompletionTermAsync(
            appointment,
            term,
            ServiceCompletionAcceptanceMethod.SignatureName,
            DateTime.UtcNow,
            signatureName);

        return new ServiceCompletionPinResultDto(true, MapCompletionTermToDto(term));
    }

    public async Task<ServiceCompletionPinResultDto> ContestCompletionAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        ContestServiceCompletionRequestDto request)
    {
        if (!IsClientRole(actorRole) && !IsAdminRole(actorRole))
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "forbidden",
                ErrorMessage: "Apenas cliente ou admin podem contestar a conclusao.");
        }

        if (appointmentId == Guid.Empty)
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "invalid_appointment",
                ErrorMessage: "Agendamento invalido.");
        }

        var reason = request?.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 5)
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "contest_reason_required",
                ErrorMessage: "Informe o motivo da contestacao com ao menos 5 caracteres.");
        }

        var appointment = await _serviceAppointmentRepository.GetByIdAsync(appointmentId);
        if (appointment == null)
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "appointment_not_found",
                ErrorMessage: "Agendamento nao encontrado.");
        }

        if (!IsAdminRole(actorRole) && appointment.ClientId != actorUserId)
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "forbidden",
                ErrorMessage: "Cliente sem permissao para contestar este agendamento.");
        }

        var term = await _serviceCompletionTermRepository.GetByAppointmentIdAsync(appointmentId);
        if (term == null)
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "completion_term_not_found",
                ErrorMessage: "Termo de conclusao nao encontrado para este agendamento.");
        }

        if (term.Status != ServiceCompletionTermStatus.PendingClientAcceptance)
        {
            return new ServiceCompletionPinResultDto(
                false,
                Term: MapCompletionTermToDto(term),
                ErrorCode: "invalid_state",
                ErrorMessage: "Este termo nao esta mais aguardando aceite para contestacao.");
        }

        var nowUtc = DateTime.UtcNow;
        term.Status = ServiceCompletionTermStatus.ContestedByClient;
        term.ContestReason = reason;
        term.ContestedAtUtc = nowUtc;
        term.AcceptancePinHashSha256 = null;
        term.AcceptancePinExpiresAtUtc = null;
        term.UpdatedAt = nowUtc;
        await _serviceCompletionTermRepository.UpdateAsync(term);

        await _notificationService.SendNotificationAsync(
            appointment.ClientId.ToString("N"),
            "Agendamento: contestacao registrada",
            "Sua contestacao da conclusao foi registrada. O caso sera analisado.",
            BuildActionUrl(appointment.ServiceRequestId));

        await _notificationService.SendNotificationAsync(
            appointment.ProviderId.ToString("N"),
            "Agendamento: conclusao contestada",
            "O cliente contestou a conclusao do servico. Aguarde analise.",
            BuildActionUrl(appointment.ServiceRequestId));

        var admins = await _userRepository.GetAllAsync() ?? Enumerable.Empty<User>();
        var adminRecipients = admins
            .Where(u => u.IsActive && u.Role == UserRole.Admin)
            .Select(u => u.Id.ToString("N"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var adminRecipient in adminRecipients)
        {
            await _notificationService.SendNotificationAsync(
                adminRecipient,
                "Agendamento: contestacao para analise",
                $"Cliente contestou a conclusao do servico. Motivo: {reason}",
                BuildActionUrl(appointment.ServiceRequestId));
        }

        return new ServiceCompletionPinResultDto(true, MapCompletionTermToDto(term));
    }

    public async Task<ServiceCompletionPinResultDto> GetCompletionTermAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId)
    {
        if (!IsClientRole(actorRole) && !IsProviderRole(actorRole) && !IsAdminRole(actorRole))
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "forbidden",
                ErrorMessage: "Perfil sem permissao para consultar termo de conclusao.");
        }

        if (appointmentId == Guid.Empty)
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "invalid_appointment",
                ErrorMessage: "Agendamento invalido.");
        }

        var appointment = await _serviceAppointmentRepository.GetByIdAsync(appointmentId);
        if (appointment == null)
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "appointment_not_found",
                ErrorMessage: "Agendamento nao encontrado.");
        }

        if (!IsAdminRole(actorRole))
        {
            var isAuthorized = IsClientRole(actorRole)
                ? appointment.ClientId == actorUserId
                : appointment.ProviderId == actorUserId;

            if (!isAuthorized)
            {
                return new ServiceCompletionPinResultDto(
                    false,
                    ErrorCode: "forbidden",
                    ErrorMessage: "Usuario sem permissao para consultar este termo.");
            }
        }

        var term = await _serviceCompletionTermRepository.GetByAppointmentIdAsync(appointmentId);
        if (term == null)
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "completion_term_not_found",
                ErrorMessage: "Termo de conclusao nao encontrado para este agendamento.");
        }

        return new ServiceCompletionPinResultDto(true, MapCompletionTermToDto(term));
    }

    private async Task AcceptCompletionTermAsync(
        ServiceAppointment appointment,
        ServiceCompletionTerm term,
        ServiceCompletionAcceptanceMethod method,
        DateTime nowUtc,
        string? acceptedSignatureName)
    {
        term.Status = ServiceCompletionTermStatus.AcceptedByClient;
        term.AcceptedWithMethod = method;
        term.AcceptedSignatureName = method == ServiceCompletionAcceptanceMethod.SignatureName
            ? acceptedSignatureName?.Trim()
            : null;
        term.AcceptedAtUtc = nowUtc;
        term.AcceptancePinHashSha256 = null;
        term.AcceptancePinExpiresAtUtc = null;
        term.UpdatedAt = nowUtc;
        await _serviceCompletionTermRepository.UpdateAsync(term);

        var serviceRequest = appointment.ServiceRequest ?? await _serviceRequestRepository.GetByIdAsync(appointment.ServiceRequestId);
        if (serviceRequest != null &&
            serviceRequest.Status == ServiceRequestStatus.PendingClientCompletionAcceptance)
        {
            serviceRequest.Status = ServiceRequestStatus.Completed;
            await _serviceRequestRepository.UpdateAsync(serviceRequest);
        }

        var clientMessage = method == ServiceCompletionAcceptanceMethod.Pin
            ? "Sua validacao por PIN foi registrada e o servico foi concluido."
            : "Sua assinatura de aceite foi registrada e o servico foi concluido.";

        var providerMessage = method == ServiceCompletionAcceptanceMethod.Pin
            ? "O cliente validou o PIN e aceitou a conclusao do servico."
            : "O cliente assinou o aceite e confirmou a conclusao do servico.";

        await _notificationService.SendNotificationAsync(
            appointment.ClientId.ToString("N"),
            "Agendamento: conclusao aceita",
            clientMessage,
            BuildActionUrl(appointment.ServiceRequestId));

        await _notificationService.SendNotificationAsync(
            appointment.ProviderId.ToString("N"),
            "Agendamento: conclusao aceita pelo cliente",
            providerMessage,
            BuildActionUrl(appointment.ServiceRequestId));
    }

    public async Task<int> ExpirePendingAppointmentsAsync(int batchSize = 200)
    {
        var nowUtc = DateTime.UtcNow;
        var expiredCandidates = await _serviceAppointmentRepository.GetExpiredPendingAppointmentsAsync(nowUtc, batchSize);
        if (expiredCandidates.Count == 0)
        {
            return 0;
        }

        var processed = 0;
        foreach (var appointment in expiredCandidates)
        {
            if (appointment.Status != ServiceAppointmentStatus.PendingProviderConfirmation)
            {
                continue;
            }

            var previousStatus = appointment.Status;
            appointment.Status = ServiceAppointmentStatus.ExpiredWithoutProviderAction;
            appointment.ExpiresAtUtc = null;
            appointment.Reason = "Agendamento expirado automaticamente por falta de confirmacao no prazo.";
            appointment.UpdatedAt = nowUtc;
            await _serviceAppointmentRepository.UpdateAsync(appointment);

            await _serviceAppointmentRepository.AddHistoryAsync(new ServiceAppointmentHistory
            {
                ServiceAppointmentId = appointment.Id,
                PreviousStatus = previousStatus,
                NewStatus = ServiceAppointmentStatus.ExpiredWithoutProviderAction,
                ActorRole = ServiceAppointmentActorRole.System,
                Reason = "Expiracao automatica por SLA de confirmacao."
            });

            await SyncServiceRequestSchedulingStatusAsync(appointment.ServiceRequest);
            await ApplyFinancialPolicyForAppointmentEventAsync(
                appointment,
                ServiceFinancialPolicyEventType.ProviderNoShow,
                null,
                nowUtc,
                "expire_pending_confirmation",
                "Expiracao automatica por SLA de confirmacao.");

            await _notificationService.SendNotificationAsync(
                appointment.ClientId.ToString("N"),
                "Agendamento expirado",
                "Seu agendamento expirou por falta de confirmacao do prestador.",
                BuildActionUrl(appointment.ServiceRequestId));

            await _notificationService.SendNotificationAsync(
                appointment.ProviderId.ToString("N"),
                "Agendamento expirado",
                "Um agendamento pendente expirou por falta de confirmacao no prazo.",
                BuildActionUrl(appointment.ServiceRequestId));

            processed++;
        }

        return processed;
    }

    public async Task<int> ExpirePendingScopeChangeRequestsAsync(int batchSize = 200)
    {
        var nowUtc = DateTime.UtcNow;
        var timeoutThresholdUtc = nowUtc.AddMinutes(-_scopeChangeClientApprovalTimeoutMinutes);
        var expiredCandidates = await _scopeChangeRequestRepository.GetExpiredPendingByRequestedAtAsync(
            timeoutThresholdUtc,
            batchSize);
        if (expiredCandidates.Count == 0)
        {
            return 0;
        }

        var processed = 0;
        foreach (var scopeChangeRequest in expiredCandidates)
        {
            var requestLock = await AcquireServiceRequestScopeChangeLockAsync(scopeChangeRequest.ServiceRequestId);
            try
            {
                var latestScopeChange = await _scopeChangeRequestRepository.GetByIdAsync(scopeChangeRequest.Id);
                if (latestScopeChange == null ||
                    latestScopeChange.Status != ServiceScopeChangeRequestStatus.PendingClientApproval ||
                    !IsScopeChangeRequestClientApprovalTimedOut(latestScopeChange, nowUtc))
                {
                    continue;
                }

                var appointment = scopeChangeRequest.ServiceAppointment ??
                                  await _serviceAppointmentRepository.GetByIdAsync(latestScopeChange.ServiceAppointmentId);
                if (appointment == null)
                {
                    continue;
                }

                var expired = await ExpireScopeChangeRequestByTimeoutAsync(
                    appointment,
                    latestScopeChange,
                    nowUtc,
                    "Expiracao automatica por timeout de resposta do cliente.");
                if (expired)
                {
                    processed++;
                }
            }
            finally
            {
                requestLock.Release();
            }
        }

        return processed;
    }

    public async Task<ServiceAppointmentOperationResultDto> GetByIdAsync(Guid actorUserId, string actorRole, Guid appointmentId)
    {
        var appointment = await _serviceAppointmentRepository.GetByIdAsync(appointmentId);
        if (appointment == null)
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "appointment_not_found", ErrorMessage: "Agendamento nao encontrado.");
        }

        if (!CanAccessAppointment(appointment, actorUserId, actorRole))
        {
            return new ServiceAppointmentOperationResultDto(false, ErrorCode: "forbidden", ErrorMessage: "Sem permissao para acessar este agendamento.");
        }

        return new ServiceAppointmentOperationResultDto(true, MapToDto(appointment));
    }

    public async Task<IReadOnlyList<ServiceAppointmentDto>> GetMyAppointmentsAsync(
        Guid actorUserId,
        string actorRole,
        DateTime? fromUtc = null,
        DateTime? toUtc = null)
    {
        DateTime? normalizedFromUtc = fromUtc.HasValue ? NormalizeToUtc(fromUtc.Value) : null;
        DateTime? normalizedToUtc = toUtc.HasValue ? NormalizeToUtc(toUtc.Value) : null;

        IReadOnlyList<ServiceAppointment> appointments;
        if (IsProviderRole(actorRole))
        {
            appointments = await _serviceAppointmentRepository.GetByProviderAsync(actorUserId, normalizedFromUtc, normalizedToUtc);
        }
        else if (IsClientRole(actorRole))
        {
            appointments = await _serviceAppointmentRepository.GetByClientAsync(actorUserId, normalizedFromUtc, normalizedToUtc);
        }
        else if (IsAdminRole(actorRole))
        {
            appointments = Array.Empty<ServiceAppointment>();
        }
        else
        {
            appointments = Array.Empty<ServiceAppointment>();
        }

        return appointments.Select(MapToDto).ToList();
    }

    private async Task SyncServiceRequestSchedulingStatusAsync(ServiceRequest serviceRequest)
    {
        if (IsServiceRequestClosedForScheduling(serviceRequest.Status))
        {
            return;
        }

        var requestAppointments = await _serviceAppointmentRepository.GetByRequestIdAsync(serviceRequest.Id)
            ?? Array.Empty<ServiceAppointment>();
        var hasActiveAppointments = requestAppointments.Any(a => IsSchedulingActiveStatus(a.Status));
        var targetStatus = hasActiveAppointments
            ? (serviceRequest.Status == ServiceRequestStatus.InProgress
                ? ServiceRequestStatus.InProgress
                : ServiceRequestStatus.Scheduled)
            : ServiceRequestStatus.Matching;

        if (serviceRequest.Status == targetStatus)
        {
            return;
        }

        serviceRequest.Status = targetStatus;
        await _serviceRequestRepository.UpdateAsync(serviceRequest);
    }

    private static bool IsSchedulingActiveStatus(ServiceAppointmentStatus status)
    {
        return BlockingStatuses.Contains(status);
    }

    private static bool CanRespondPresenceForStatus(ServiceAppointmentStatus status)
    {
        return status is not ServiceAppointmentStatus.CancelledByClient
            and not ServiceAppointmentStatus.CancelledByProvider
            and not ServiceAppointmentStatus.RejectedByProvider
            and not ServiceAppointmentStatus.ExpiredWithoutProviderAction
            and not ServiceAppointmentStatus.Completed;
    }

    private static bool IsScopeChangeCreationAllowedStatus(ServiceAppointmentStatus status)
    {
        return status is ServiceAppointmentStatus.Arrived
            or ServiceAppointmentStatus.InProgress
            or ServiceAppointmentStatus.Confirmed
            or ServiceAppointmentStatus.RescheduleConfirmed;
    }

    private static bool IsServiceRequestClosedForScheduling(ServiceRequestStatus status)
    {
        return status is
            ServiceRequestStatus.Canceled or
            ServiceRequestStatus.Completed or
            ServiceRequestStatus.Validated or
            ServiceRequestStatus.PendingClientCompletionAcceptance;
    }

    private async Task<ServiceCompletionPinResultDto> UpsertCompletionTermPinAsync(
        ServiceAppointment appointment,
        bool forceRegenerate,
        string? reason,
        DateTime nowUtc)
    {
        if (appointment.Status != ServiceAppointmentStatus.Completed)
        {
            return new ServiceCompletionPinResultDto(
                false,
                ErrorCode: "invalid_state",
                ErrorMessage: "PIN de aceite so pode ser gerado para agendamento concluido.");
        }

        var existing = await _serviceCompletionTermRepository.GetByAppointmentIdAsync(appointment.Id);
        var canReuseCurrentPin =
            !forceRegenerate &&
            existing != null &&
            existing.Status == ServiceCompletionTermStatus.PendingClientAcceptance &&
            !string.IsNullOrWhiteSpace(existing.AcceptancePinHashSha256) &&
            existing.AcceptancePinExpiresAtUtc.HasValue &&
            existing.AcceptancePinExpiresAtUtc.Value > nowUtc;

        if (canReuseCurrentPin && existing != null)
        {
            return new ServiceCompletionPinResultDto(true, MapCompletionTermToDto(existing));
        }

        var pin = GenerateNumericPin(_completionPinLength);
        var pinHash = BuildCompletionPinHash(appointment.Id, pin);
        var expiresAtUtc = nowUtc.AddMinutes(_completionPinExpiryMinutes);
        var summary = BuildCompletionSummary(appointment, reason);
        var payloadJson = BuildCompletionPayloadJson(appointment, summary, reason);
        var payloadHash = BuildPayloadHash(appointment.Id, payloadJson);

        if (existing == null)
        {
            var created = new ServiceCompletionTerm
            {
                ServiceRequestId = appointment.ServiceRequestId,
                ServiceAppointmentId = appointment.Id,
                ProviderId = appointment.ProviderId,
                ClientId = appointment.ClientId,
                Status = ServiceCompletionTermStatus.PendingClientAcceptance,
                Summary = summary,
                PayloadJson = payloadJson,
                PayloadHashSha256 = payloadHash,
                MetadataJson = BuildCompletionMetadataJson(reason),
                AcceptancePinHashSha256 = pinHash,
                AcceptancePinExpiresAtUtc = expiresAtUtc,
                AcceptancePinFailedAttempts = 0,
                AcceptedWithMethod = null,
                AcceptedSignatureName = null,
                AcceptedAtUtc = null,
                ContestReason = null,
                ContestedAtUtc = null,
                EscalatedAtUtc = null
            };

            await _serviceCompletionTermRepository.AddAsync(created);
            return new ServiceCompletionPinResultDto(true, MapCompletionTermToDto(created), pin);
        }

        existing.Status = ServiceCompletionTermStatus.PendingClientAcceptance;
        existing.Summary = summary;
        existing.PayloadJson = payloadJson;
        existing.PayloadHashSha256 = payloadHash;
        existing.MetadataJson = BuildCompletionMetadataJson(reason);
        existing.AcceptancePinHashSha256 = pinHash;
        existing.AcceptancePinExpiresAtUtc = expiresAtUtc;
        existing.AcceptancePinFailedAttempts = 0;
        existing.AcceptedWithMethod = null;
        existing.AcceptedSignatureName = null;
        existing.AcceptedAtUtc = null;
        existing.ContestReason = null;
        existing.ContestedAtUtc = null;
        existing.EscalatedAtUtc = null;
        existing.UpdatedAt = nowUtc;

        await _serviceCompletionTermRepository.UpdateAsync(existing);
        return new ServiceCompletionPinResultDto(true, MapCompletionTermToDto(existing), pin);
    }

    private string? NormalizePin(string? pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
        {
            return null;
        }

        var normalized = pin.Trim();
        if (normalized.Length != _completionPinLength || normalized.Any(c => c is < '0' or > '9'))
        {
            return null;
        }

        return normalized;
    }

    private static string GenerateNumericPin(int length)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));
        }

        return new string(chars);
    }

    private static string BuildCompletionPinHash(Guid appointmentId, string pin)
    {
        var payload = $"{appointmentId:N}:{pin}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string BuildPayloadHash(Guid appointmentId, string payloadJson)
    {
        var payload = $"{appointmentId:N}:{payloadJson}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static bool FixedTimeEqualsHex(string leftHex, string rightHex)
    {
        if (leftHex.Length != rightHex.Length)
        {
            return false;
        }

        try
        {
            var left = Convert.FromHexString(leftHex);
            var right = Convert.FromHexString(rightHex);
            return CryptographicOperations.FixedTimeEquals(left, right);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string BuildCompletionSummary(ServiceAppointment appointment, string? reason)
    {
        var started = appointment.StartedAtUtc?.ToString("dd/MM/yyyy HH:mm") ?? "-";
        var completed = appointment.CompletedAtUtc?.ToString("dd/MM/yyyy HH:mm") ?? "-";
        var reasonText = string.IsNullOrWhiteSpace(reason) ? "-" : reason.Trim();
        return $"Atendimento encerrado. Inicio: {started}. Conclusao: {completed}. Observacao: {reasonText}.";
    }

    private static string BuildCompletionPayloadJson(ServiceAppointment appointment, string summary, string? reason)
    {
        return JsonSerializer.Serialize(new
        {
            appointmentId = appointment.Id,
            serviceRequestId = appointment.ServiceRequestId,
            providerId = appointment.ProviderId,
            clientId = appointment.ClientId,
            status = appointment.Status.ToString(),
            operationalStatus = appointment.OperationalStatus?.ToString(),
            startedAtUtc = appointment.StartedAtUtc,
            completedAtUtc = appointment.CompletedAtUtc,
            reason,
            summary
        });
    }

    private static string? BuildCompletionMetadataJson(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        return JsonSerializer.Serialize(new
        {
            source = "operational-status",
            reason = reason.Trim()
        });
    }

    private bool IsScopeChangeRequestClientApprovalTimedOut(ServiceScopeChangeRequest scopeChangeRequest, DateTime nowUtc)
    {
        if (scopeChangeRequest.Status != ServiceScopeChangeRequestStatus.PendingClientApproval)
        {
            return false;
        }

        var normalizedRequestedAtUtc = NormalizeToUtc(scopeChangeRequest.RequestedAtUtc);
        var timeoutThresholdUtc = nowUtc.AddMinutes(-_scopeChangeClientApprovalTimeoutMinutes);
        return normalizedRequestedAtUtc <= timeoutThresholdUtc;
    }

    private async Task<bool> ExpireScopeChangeRequestByTimeoutAsync(
        ServiceAppointment appointment,
        ServiceScopeChangeRequest scopeChangeRequest,
        DateTime nowUtc,
        string reason)
    {
        if (scopeChangeRequest.Status != ServiceScopeChangeRequestStatus.PendingClientApproval ||
            !IsScopeChangeRequestClientApprovalTimedOut(scopeChangeRequest, nowUtc))
        {
            return false;
        }

        scopeChangeRequest.Status = ServiceScopeChangeRequestStatus.Expired;
        scopeChangeRequest.ClientRespondedAtUtc = nowUtc;
        scopeChangeRequest.ClientResponseReason = "Expirado automaticamente por falta de resposta do cliente no prazo.";
        scopeChangeRequest.UpdatedAt = nowUtc;
        await _scopeChangeRequestRepository.UpdateAsync(scopeChangeRequest);

        await AppendScopeChangeAuditHistoryAsync(
            appointment,
            Guid.Empty,
            "System",
            scopeChangeRequest,
            "expired",
            reason);

        var serviceRequest = appointment.ServiceRequest
            ?? await _serviceRequestRepository.GetByIdAsync(scopeChangeRequest.ServiceRequestId);
        if (serviceRequest != null)
        {
            var commercialTotals = await _serviceRequestCommercialValueService.RecalculateAsync(serviceRequest);
            serviceRequest.CommercialVersion = Math.Max(1, serviceRequest.CommercialVersion);
            serviceRequest.CommercialBaseValue = commercialTotals.BaseValue;
            serviceRequest.CommercialCurrentValue = commercialTotals.CurrentValue;
            serviceRequest.CommercialState = ServiceRequestCommercialState.Stable;
            serviceRequest.CommercialUpdatedAtUtc = nowUtc;
            await _serviceRequestRepository.UpdateAsync(serviceRequest);
        }

        var actionUrl = $"{BuildActionUrl(scopeChangeRequest.ServiceRequestId)}?scopeChangeId={scopeChangeRequest.Id}";
        await _notificationService.SendNotificationAsync(
            appointment.ProviderId.ToString("N"),
            "Aditivo expirado",
            $"Aditivo v{scopeChangeRequest.Version} expirou por falta de resposta do cliente no prazo.",
            actionUrl);

        await _notificationService.SendNotificationAsync(
            appointment.ClientId.ToString("N"),
            "Aditivo expirado",
            $"O aditivo v{scopeChangeRequest.Version} expirou por tempo limite de resposta.",
            actionUrl);

        return true;
    }

    private async Task AppendScopeChangeAuditHistoryAsync(
        ServiceAppointment appointment,
        Guid actorUserId,
        string actorRole,
        ServiceScopeChangeRequest scopeChangeRequest,
        string action,
        string? reason = null)
    {
        var metadata = JsonSerializer.Serialize(new
        {
            type = "scope_change_audit",
            action,
            scopeChangeRequestId = scopeChangeRequest.Id,
            scopeChangeVersion = scopeChangeRequest.Version,
            scopeChangeStatus = scopeChangeRequest.Status.ToString(),
            scopeChangeRequest.ServiceRequestId,
            scopeChangeRequest.ServiceAppointmentId,
            scopeChangeRequest.ProviderId,
            scopeChangeRequest.IncrementalValue,
            reason
        });

        await _serviceAppointmentRepository.AddHistoryAsync(new ServiceAppointmentHistory
        {
            ServiceAppointmentId = appointment.Id,
            PreviousStatus = appointment.Status,
            NewStatus = appointment.Status,
            PreviousOperationalStatus = appointment.OperationalStatus,
            NewOperationalStatus = appointment.OperationalStatus,
            ActorUserId = actorUserId,
            ActorRole = ResolveActorRole(actorRole),
            Reason = BuildScopeChangeAuditReason(action, scopeChangeRequest.Version, reason),
            Metadata = metadata
        });
    }

    private static string BuildScopeChangeAuditReason(string action, int version, string? reason)
    {
        return action switch
        {
            "created" => $"Aditivo v{version} solicitado.",
            "attachment_added" => $"Anexo adicionado ao aditivo v{version}: {reason}.",
            "approved" => $"Aditivo v{version} aprovado pelo cliente.",
            "rejected" => string.IsNullOrWhiteSpace(reason)
                ? $"Aditivo v{version} rejeitado pelo cliente."
                : $"Aditivo v{version} rejeitado pelo cliente. Motivo: {reason}.",
            "expired" => $"Aditivo v{version} expirado por timeout de resposta do cliente.",
            _ => $"Aditivo v{version} atualizado."
        };
    }

    private static ServiceCompletionTermDto MapCompletionTermToDto(ServiceCompletionTerm term)
    {
        return new ServiceCompletionTermDto(
            term.Id,
            term.ServiceRequestId,
            term.ServiceAppointmentId,
            term.ProviderId,
            term.ClientId,
            term.Status.ToString(),
            term.AcceptedWithMethod?.ToString(),
            term.AcceptancePinExpiresAtUtc,
            term.AcceptancePinFailedAttempts,
            term.AcceptedAtUtc,
            term.ContestedAtUtc,
            term.EscalatedAtUtc,
            term.CreatedAt,
            term.UpdatedAt,
            term.Summary,
            term.AcceptedSignatureName,
            term.ContestReason);
    }

    private static ServiceScopeChangeRequestDto MapScopeChangeRequestToDto(ServiceScopeChangeRequest scopeChangeRequest)
    {
        var attachments = (scopeChangeRequest.Attachments ?? Array.Empty<ServiceScopeChangeRequestAttachment>())
            .OrderByDescending(a => a.CreatedAt)
            .Select(MapScopeChangeAttachmentToDto)
            .ToList();

        return new ServiceScopeChangeRequestDto(
            scopeChangeRequest.Id,
            scopeChangeRequest.ServiceRequestId,
            scopeChangeRequest.ServiceAppointmentId,
            scopeChangeRequest.ProviderId,
            scopeChangeRequest.Version,
            scopeChangeRequest.Status.ToString(),
            scopeChangeRequest.Reason,
            scopeChangeRequest.AdditionalScopeDescription,
            scopeChangeRequest.IncrementalValue,
            scopeChangeRequest.RequestedAtUtc,
            scopeChangeRequest.ClientRespondedAtUtc,
            scopeChangeRequest.ClientResponseReason,
            scopeChangeRequest.PreviousVersionId,
            scopeChangeRequest.CreatedAt,
            scopeChangeRequest.UpdatedAt,
            attachments);
    }

    private static ServiceScopeChangeAttachmentDto MapScopeChangeAttachmentToDto(ServiceScopeChangeRequestAttachment attachment)
    {
        return new ServiceScopeChangeAttachmentDto(
            attachment.Id,
            attachment.ServiceScopeChangeRequestId,
            attachment.UploadedByUserId,
            attachment.FileUrl,
            attachment.FileName,
            attachment.ContentType,
            attachment.MediaKind,
            attachment.SizeBytes,
            attachment.CreatedAt);
    }

    private static async Task<IReadOnlyList<SemaphoreSlim>> AcquireCreationLocksAsync(IEnumerable<string> keys)
    {
        var normalizedKeys = keys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        var acquiredLocks = new List<SemaphoreSlim>(normalizedKeys.Count);
        foreach (var key in normalizedKeys)
        {
            var lockInstance = AppointmentCreationLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await lockInstance.WaitAsync();
            acquiredLocks.Add(lockInstance);
        }

        return acquiredLocks;
    }

    private static void ReleaseCreationLocks(IReadOnlyList<SemaphoreSlim> locks)
    {
        for (var i = locks.Count - 1; i >= 0; i--)
        {
            locks[i].Release();
        }
    }

    private static async Task<SemaphoreSlim> AcquireAppointmentOperationalLockAsync(Guid appointmentId)
    {
        var lockInstance = AppointmentOperationalLocks.GetOrAdd(appointmentId, _ => new SemaphoreSlim(1, 1));
        await lockInstance.WaitAsync();
        return lockInstance;
    }

    private static async Task<SemaphoreSlim> AcquireServiceRequestScopeChangeLockAsync(Guid serviceRequestId)
    {
        var lockInstance = ServiceRequestScopeChangeLocks.GetOrAdd(serviceRequestId, _ => new SemaphoreSlim(1, 1));
        await lockInstance.WaitAsync();
        return lockInstance;
    }

    private static bool CanTransitionOperationalStatus(
        ServiceAppointmentOperationalStatus? currentStatus,
        ServiceAppointmentOperationalStatus targetStatus)
    {
        if (!currentStatus.HasValue)
        {
            return targetStatus == ServiceAppointmentOperationalStatus.OnTheWay;
        }

        if (currentStatus.Value == targetStatus)
        {
            return true;
        }

        return OperationalStatusTransitions.TryGetValue(currentStatus.Value, out var validTargets) &&
               validTargets.Contains(targetStatus);
    }

    private static ServiceAppointmentOperationalStatus? ResolveCurrentOperationalStatus(ServiceAppointment appointment)
    {
        if (appointment.OperationalStatus.HasValue)
        {
            return appointment.OperationalStatus.Value;
        }

        if (appointment.Status == ServiceAppointmentStatus.Completed)
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

        if (appointment.Status == ServiceAppointmentStatus.Confirmed || appointment.Status == ServiceAppointmentStatus.RescheduleConfirmed)
        {
            return ServiceAppointmentOperationalStatus.OnTheWay;
        }

        return null;
    }

    private async Task<bool> IsSlotAvailableForProviderAsync(
        Guid providerId,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        Guid? excludedAppointmentId = null)
    {
        var rules = await _serviceAppointmentRepository.GetAvailabilityRulesByProviderAsync(providerId);
        if (rules.Count == 0)
        {
            return false;
        }

        var windowStartLocal = ConvertUtcToAvailabilityLocal(windowStartUtc, _availabilityTimeZone);
        var windowEndLocal = ConvertUtcToAvailabilityLocal(windowEndUtc, _availabilityTimeZone);
        if (windowEndLocal <= windowStartLocal || windowStartLocal.Date != windowEndLocal.Date)
        {
            return false;
        }

        var isInsideAnyRule = rules.Any(r =>
            r.DayOfWeek == windowStartLocal.DayOfWeek &&
            r.StartTime <= windowStartLocal.TimeOfDay &&
            r.EndTime >= windowEndLocal.TimeOfDay);

        if (!isInsideAnyRule)
        {
            return false;
        }

        var exceptions = await _serviceAppointmentRepository.GetAvailabilityExceptionsByProviderAsync(providerId, windowStartUtc, windowEndUtc);
        if (exceptions.Any(e => Overlaps(windowStartUtc, windowEndUtc, e.StartsAtUtc, e.EndsAtUtc)))
        {
            return false;
        }

        var conflictingAppointments = await _serviceAppointmentRepository.GetProviderAppointmentsByStatusesInRangeAsync(
            providerId,
            windowStartUtc,
            windowEndUtc,
            BlockingStatuses);

        return !conflictingAppointments.Any(a =>
            (!excludedAppointmentId.HasValue || a.Id != excludedAppointmentId.Value) &&
            Overlaps(windowStartUtc, windowEndUtc, a.WindowStartUtc, a.WindowEndUtc));
    }

    private IReadOnlyList<ServiceAppointmentSlotDto> BuildAvailableSlots(
        IReadOnlyList<ProviderAvailabilityRule> rules,
        IReadOnlyList<ProviderAvailabilityException> exceptions,
        IReadOnlyList<ServiceAppointment> reservedAppointments,
        DateTime rangeStartUtc,
        DateTime rangeEndUtc,
        int? requestedSlotDurationMinutes)
    {
        rangeStartUtc = NormalizeToUtc(rangeStartUtc);
        rangeEndUtc = NormalizeToUtc(rangeEndUtc);

        var rangeStartLocal = ConvertUtcToAvailabilityLocal(rangeStartUtc, _availabilityTimeZone);
        var rangeEndLocal = ConvertUtcToAvailabilityLocal(rangeEndUtc, _availabilityTimeZone);
        var dedup = new HashSet<string>(StringComparer.Ordinal);
        var slots = new List<ServiceAppointmentSlotDto>();

        for (var dayLocal = rangeStartLocal.Date; dayLocal <= rangeEndLocal.Date; dayLocal = dayLocal.AddDays(1))
        {
            foreach (var rule in rules.Where(r => r.DayOfWeek == dayLocal.DayOfWeek))
            {
                var slotDurationMinutes = requestedSlotDurationMinutes ?? rule.SlotDurationMinutes;
                if (slotDurationMinutes < MinimumSlotDurationMinutes || slotDurationMinutes > MaximumSlotDurationMinutes)
                {
                    continue;
                }

                var ruleStartLocal = dayLocal.Add(rule.StartTime);
                var ruleEndLocal = dayLocal.Add(rule.EndTime);
                var ruleStartUtc = ConvertAvailabilityLocalToUtc(ruleStartLocal, _availabilityTimeZone);
                var ruleEndUtc = ConvertAvailabilityLocalToUtc(ruleEndLocal, _availabilityTimeZone);

                if (ruleEndUtc <= rangeStartUtc || ruleStartUtc >= rangeEndUtc)
                {
                    continue;
                }

                for (var cursor = ruleStartUtc; cursor.AddMinutes(slotDurationMinutes) <= ruleEndUtc; cursor = cursor.AddMinutes(slotDurationMinutes))
                {
                    var candidateStartUtc = cursor;
                    var candidateEndUtc = cursor.AddMinutes(slotDurationMinutes);

                    if (candidateStartUtc < rangeStartUtc || candidateEndUtc > rangeEndUtc)
                    {
                        continue;
                    }

                    if (exceptions.Any(e => Overlaps(candidateStartUtc, candidateEndUtc, e.StartsAtUtc, e.EndsAtUtc)))
                    {
                        continue;
                    }

                    if (reservedAppointments.Any(a => Overlaps(candidateStartUtc, candidateEndUtc, a.WindowStartUtc, a.WindowEndUtc)))
                    {
                        continue;
                    }

                    var dedupKey = $"{candidateStartUtc:O}|{candidateEndUtc:O}";
                    if (dedup.Add(dedupKey))
                    {
                        slots.Add(new ServiceAppointmentSlotDto(candidateStartUtc, candidateEndUtc));
                    }
                }
            }
        }

        return slots
            .OrderBy(s => s.WindowStartUtc)
            .ToList();
    }

    private static ServiceAppointmentDto MapToDto(ServiceAppointment appointment)
    {
        return new ServiceAppointmentDto(
            appointment.Id,
            appointment.ServiceRequestId,
            appointment.ClientId,
            appointment.ProviderId,
            appointment.Status.ToString(),
            appointment.WindowStartUtc,
            appointment.WindowEndUtc,
            appointment.ExpiresAtUtc,
            appointment.Reason,
            appointment.ProposedWindowStartUtc,
            appointment.ProposedWindowEndUtc,
            appointment.RescheduleRequestedAtUtc,
            appointment.RescheduleRequestedByRole?.ToString(),
            appointment.RescheduleRequestReason,
            appointment.CreatedAt,
            appointment.UpdatedAt,
            appointment.History
                .OrderBy(h => h.OccurredAtUtc)
                .Select(h => new ServiceAppointmentHistoryDto(
                    h.Id,
                    h.PreviousStatus?.ToString(),
                    h.NewStatus.ToString(),
                    h.ActorUserId,
                    h.ActorRole.ToString(),
                    h.Reason,
                    h.OccurredAtUtc,
                    h.PreviousOperationalStatus?.ToString(),
                    h.NewOperationalStatus?.ToString(),
                    h.Metadata))
                .ToList(),
            appointment.ArrivedAtUtc,
            appointment.ArrivedLatitude,
            appointment.ArrivedLongitude,
            appointment.ArrivedAccuracyMeters,
            appointment.ArrivedManualReason,
            appointment.StartedAtUtc,
            appointment.OperationalStatus?.ToString(),
            appointment.OperationalStatusUpdatedAtUtc,
            appointment.OperationalStatusReason,
            appointment.ClientPresenceConfirmed,
            appointment.ClientPresenceRespondedAtUtc,
            appointment.ClientPresenceReason,
            appointment.ProviderPresenceConfirmed,
            appointment.ProviderPresenceRespondedAtUtc,
            appointment.ProviderPresenceReason,
            appointment.NoShowRiskScore,
            appointment.NoShowRiskLevel?.ToString(),
            appointment.NoShowRiskCalculatedAtUtc,
            appointment.NoShowRiskReasons);
    }

    private static ProviderAvailabilityRuleDto MapAvailabilityRuleToDto(ProviderAvailabilityRule rule)
    {
        return new ProviderAvailabilityRuleDto(
            rule.Id,
            rule.ProviderId,
            rule.DayOfWeek,
            rule.StartTime,
            rule.EndTime,
            rule.SlotDurationMinutes,
            rule.IsActive,
            rule.CreatedAt,
            rule.UpdatedAt);
    }

    private static ProviderAvailabilityExceptionDto MapAvailabilityExceptionToDto(ProviderAvailabilityException exception)
    {
        return new ProviderAvailabilityExceptionDto(
            exception.Id,
            exception.ProviderId,
            exception.StartsAtUtc,
            exception.EndsAtUtc,
            exception.Reason,
            exception.IsActive,
            exception.CreatedAt,
            exception.UpdatedAt);
    }

    private static ServiceAppointmentStatus ResolveStatusBeforePendingReschedule(ServiceAppointment appointment)
    {
        var pendingStatus = appointment.Status;
        var previousStatus = appointment.History
            .OrderByDescending(h => h.OccurredAtUtc)
            .Where(h => h.NewStatus == pendingStatus && h.PreviousStatus.HasValue)
            .Select(h => h.PreviousStatus!.Value)
            .FirstOrDefault();

        return previousStatus is ServiceAppointmentStatus.Confirmed or ServiceAppointmentStatus.RescheduleConfirmed
            ? previousStatus
            : ServiceAppointmentStatus.Confirmed;
    }

    private static bool CanCancelFromStatus(ServiceAppointmentStatus status)
    {
        return status != ServiceAppointmentStatus.CancelledByClient &&
               status != ServiceAppointmentStatus.CancelledByProvider &&
               status != ServiceAppointmentStatus.Completed &&
               status != ServiceAppointmentStatus.Arrived &&
               status != ServiceAppointmentStatus.InProgress &&
               status != ServiceAppointmentStatus.ExpiredWithoutProviderAction &&
               status != ServiceAppointmentStatus.RejectedByProvider;
    }

    private static void ClearRescheduleProposal(ServiceAppointment appointment)
    {
        appointment.ProposedWindowStartUtc = null;
        appointment.ProposedWindowEndUtc = null;
        appointment.RescheduleRequestedAtUtc = null;
        appointment.RescheduleRequestedByRole = null;
        appointment.RescheduleRequestReason = null;
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static TimeZoneInfo ResolveAvailabilityTimeZone(string? configuredTimeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(configuredTimeZoneId))
        {
            var configuredTimeZone = TryFindTimeZone(configuredTimeZoneId.Trim());
            if (configuredTimeZone != null)
            {
                return configuredTimeZone;
            }
        }

        var preferredTimeZone = TryFindTimeZone("America/Sao_Paulo") ??
                                TryFindTimeZone("E. South America Standard Time");

        return preferredTimeZone ?? TimeZoneInfo.Local;
    }

    private static TimeZoneInfo? TryFindTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }

    private static DateTime ConvertUtcToAvailabilityLocal(DateTime utcValue, TimeZoneInfo timeZone)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(NormalizeToUtc(utcValue), timeZone);
    }

    private static DateTime ConvertAvailabilityLocalToUtc(DateTime localValue, TimeZoneInfo timeZone)
    {
        var unspecifiedLocal = DateTime.SpecifyKind(localValue, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(unspecifiedLocal))
        {
            unspecifiedLocal = unspecifiedLocal.AddHours(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(unspecifiedLocal, timeZone);
    }

    private static bool Overlaps(DateTime leftStartUtc, DateTime leftEndUtc, DateTime rightStartUtc, DateTime rightEndUtc)
    {
        return leftStartUtc < rightEndUtc && leftEndUtc > rightStartUtc;
    }

    private async Task ApplyFinancialPolicyForAppointmentEventAsync(
        ServiceAppointment appointment,
        ServiceFinancialPolicyEventType eventType,
        Guid? actorUserId,
        DateTime eventOccurredAtUtc,
        string source,
        string? reason)
    {
        if (_serviceFinancialPolicyCalculationService == null || _providerCreditService == null)
        {
            return;
        }

        try
        {
            var serviceRequest = appointment.ServiceRequest ??
                                 await _serviceRequestRepository.GetByIdAsync(appointment.ServiceRequestId);
            if (serviceRequest == null)
            {
                return;
            }

            var totals = await _serviceRequestCommercialValueService.RecalculateAsync(serviceRequest);
            var serviceValue = ResolveServiceValueForFinancialPolicy(totals);
            if (serviceValue <= 0m)
            {
                return;
            }

            var calculation = await _serviceFinancialPolicyCalculationService.CalculateAsync(
                new ServiceFinancialCalculationRequestDto(
                    eventType,
                    serviceValue,
                    appointment.WindowStartUtc,
                    eventOccurredAtUtc));

            if (!calculation.Success || calculation.Breakdown == null)
            {
                await _serviceAppointmentRepository.AddHistoryAsync(new ServiceAppointmentHistory
                {
                    ServiceAppointmentId = appointment.Id,
                    PreviousStatus = appointment.Status,
                    NewStatus = appointment.Status,
                    ActorUserId = actorUserId,
                    ActorRole = ServiceAppointmentActorRole.System,
                    Reason = "Politica financeira nao aplicada: falha no calculo.",
                    Metadata = JsonSerializer.Serialize(new
                    {
                        type = "financial_policy_calculation_failed",
                        eventType = eventType.ToString(),
                        source,
                        serviceValue,
                        calculation.ErrorCode,
                        calculation.ErrorMessage
                    })
                });

                return;
            }

            var breakdown = calculation.Breakdown;
            ProviderCreditMutationRequestDto? mutationRequest = null;
            if (string.Equals(breakdown.CounterpartyActorLabel, "Provider", StringComparison.OrdinalIgnoreCase) &&
                breakdown.CounterpartyCompensationAmount > 0m)
            {
                mutationRequest = new ProviderCreditMutationRequestDto(
                    appointment.ProviderId,
                    ProviderCreditLedgerEntryType.Grant,
                    breakdown.CounterpartyCompensationAmount,
                    $"Compensacao financeira automatica por {eventType}",
                    Source: $"FinancialPolicy:{source}",
                    ReferenceType: "ServiceAppointment",
                    ReferenceId: appointment.Id,
                    EffectiveAtUtc: eventOccurredAtUtc,
                    Metadata: JsonSerializer.Serialize(new
                    {
                        type = "financial_policy_provider_compensation",
                        appointmentId = appointment.Id,
                        serviceRequestId = appointment.ServiceRequestId,
                        eventType = eventType.ToString(),
                        reason,
                        breakdown
                    }));
            }
            else if (string.Equals(breakdown.CounterpartyActorLabel, "Client", StringComparison.OrdinalIgnoreCase) &&
                     breakdown.PenaltyAmount > 0m)
            {
                mutationRequest = new ProviderCreditMutationRequestDto(
                    appointment.ProviderId,
                    ProviderCreditLedgerEntryType.Debit,
                    breakdown.PenaltyAmount,
                    $"Penalidade financeira automatica por {eventType}",
                    Source: $"FinancialPolicy:{source}",
                    ReferenceType: "ServiceAppointment",
                    ReferenceId: appointment.Id,
                    EffectiveAtUtc: eventOccurredAtUtc,
                    Metadata: JsonSerializer.Serialize(new
                    {
                        type = "financial_policy_provider_penalty",
                        appointmentId = appointment.Id,
                        serviceRequestId = appointment.ServiceRequestId,
                        eventType = eventType.ToString(),
                        reason,
                        breakdown
                    }));
            }

            ProviderCreditMutationResultDto? mutationResult = null;
            if (mutationRequest != null)
            {
                mutationResult = await _providerCreditService.ApplyMutationAsync(
                    mutationRequest,
                    actorUserId,
                    null);
            }

            var hasLedgerImpact = mutationRequest != null;
            var ledgerSuccess = mutationResult?.Success == true;
            var historyReason = !hasLedgerImpact
                ? "Politica financeira aplicada sem impacto de ledger para o prestador."
                : ledgerSuccess
                    ? "Politica financeira aplicada com lancamento de ledger."
                    : $"Politica financeira calculada, mas falhou ao lancar no ledger ({mutationResult?.ErrorCode}).";

            await _serviceAppointmentRepository.AddHistoryAsync(new ServiceAppointmentHistory
            {
                ServiceAppointmentId = appointment.Id,
                PreviousStatus = appointment.Status,
                NewStatus = appointment.Status,
                ActorUserId = actorUserId,
                ActorRole = ServiceAppointmentActorRole.System,
                Reason = historyReason,
                Metadata = JsonSerializer.Serialize(new
                {
                    type = "financial_policy_application",
                    eventType = eventType.ToString(),
                    source,
                    reason,
                    serviceValue,
                    breakdown,
                    ledger = mutationRequest == null
                        ? null
                        : new
                        {
                            requested = new
                            {
                                mutationRequest.EntryType,
                                mutationRequest.Amount,
                                mutationRequest.Reason,
                                mutationRequest.Source,
                                mutationRequest.ReferenceType,
                                mutationRequest.ReferenceId
                            },
                            result = mutationResult == null
                                ? null
                                : new
                                {
                                    mutationResult.Success,
                                    mutationResult.ErrorCode,
                                    mutationResult.ErrorMessage
                                }
                        }
                })
            });
        }
        catch (Exception)
        {
            // Nao interromper o fluxo principal de cancelamento/expiracao por falha no fluxo financeiro.
        }
    }

    private static decimal ResolveServiceValueForFinancialPolicy(ServiceRequestCommercialTotalsDto totals)
    {
        var candidate = totals.CurrentValue > 0m
            ? totals.CurrentValue
            : totals.BaseValue > 0m
                ? totals.BaseValue
                : totals.ApprovedIncrementalValue;

        return decimal.Round(Math.Max(0m, candidate), 2, MidpointRounding.AwayFromZero);
    }

    private static string ResolveScopeAttachmentMediaKind(string contentType, string fileName)
    {
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return "image";
        }

        if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            return "video";
        }

        var extension = Path.GetExtension(fileName);
        if (string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".webp", StringComparison.OrdinalIgnoreCase))
        {
            return "image";
        }

        if (string.Equals(extension, ".mp4", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".mov", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".webm", StringComparison.OrdinalIgnoreCase))
        {
            return "video";
        }

        return "file";
    }

    private static string BuildActionUrl(Guid requestId)
    {
        return $"/ServiceRequests/Details/{requestId}";
    }

    private static ServiceAppointmentActorRole ResolveActorRole(string actorRole)
    {
        if (IsAdminRole(actorRole))
        {
            return ServiceAppointmentActorRole.Admin;
        }

        if (IsProviderRole(actorRole))
        {
            return ServiceAppointmentActorRole.Provider;
        }

        if (IsClientRole(actorRole))
        {
            return ServiceAppointmentActorRole.Client;
        }

        return ServiceAppointmentActorRole.System;
    }

    private static bool CanAccessAppointment(ServiceAppointment appointment, Guid actorUserId, string actorRole)
    {
        if (IsAdminRole(actorRole))
        {
            return true;
        }

        if (IsClientRole(actorRole))
        {
            return appointment.ClientId == actorUserId;
        }

        if (IsProviderRole(actorRole))
        {
            return appointment.ProviderId == actorUserId;
        }

        return false;
    }

    private static bool IsAdminRole(string role)
    {
        return role.Equals(UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsClientRole(string role)
    {
        return role.Equals(UserRole.Client.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProviderRole(string role)
    {
        return role.Equals(UserRole.Provider.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct ScopeChangePolicy(
        decimal MaxIncrementalValue,
        decimal MaxPercentOverAcceptedProposal);

    private IReadOnlyDictionary<ProviderPlan, ScopeChangePolicy> BuildScopeChangePolicies(IConfiguration configuration)
    {
        var policies = new Dictionary<ProviderPlan, ScopeChangePolicy>();
        foreach (var kvp in DefaultScopeChangePolicies)
        {
            var plan = kvp.Key;
            var fallback = kvp.Value;
            var prefix = $"ServiceAppointments:ScopeChangePolicies:{plan}";

            var maxIncrementalValue = ParseDecimalPolicyValue(
                configuration,
                $"{prefix}:MaxIncrementalValue",
                fallback.MaxIncrementalValue,
                minimum: 1m,
                maximum: 1000000m);
            var maxPercentOverAcceptedProposal = ParseDecimalPolicyValue(
                configuration,
                $"{prefix}:MaxPercentOverAcceptedProposal",
                fallback.MaxPercentOverAcceptedProposal,
                minimum: 1m,
                maximum: 1000m);

            policies[plan] = new ScopeChangePolicy(maxIncrementalValue, maxPercentOverAcceptedProposal);
        }

        return policies;
    }

    private ScopeChangePolicy ResolveScopeChangePolicy(ProviderPlan plan)
    {
        if (_scopeChangePolicies.TryGetValue(plan, out var configured))
        {
            return configured;
        }

        if (DefaultScopeChangePolicies.TryGetValue(plan, out var fallback))
        {
            return fallback;
        }

        return DefaultScopeChangePolicies[ProviderPlan.Trial];
    }

    private static decimal ResolveScopeChangeLimit(ScopeChangePolicy policy, decimal acceptedProposalValue)
    {
        var percentBound = acceptedProposalValue > 0m
            ? decimal.Round(
                acceptedProposalValue * (policy.MaxPercentOverAcceptedProposal / 100m),
                2,
                MidpointRounding.AwayFromZero)
            : policy.MaxIncrementalValue;

        var limit = Math.Min(policy.MaxIncrementalValue, percentBound);
        return limit <= 0m ? policy.MaxIncrementalValue : limit;
    }

    private static int ParsePolicyValue(
        IConfiguration configuration,
        string key,
        int defaultValue,
        int minimum,
        int maximum)
    {
        var configuredRaw = configuration[key];
        if (!int.TryParse(configuredRaw, out var configuredValue))
        {
            return defaultValue;
        }

        return Math.Clamp(configuredValue, minimum, maximum);
    }

    private static decimal ParseDecimalPolicyValue(
        IConfiguration configuration,
        string key,
        decimal defaultValue,
        decimal minimum,
        decimal maximum)
    {
        var configuredRaw = configuration[key];
        if (!decimal.TryParse(configuredRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var configuredValue) &&
            !decimal.TryParse(configuredRaw, NumberStyles.Number, CultureInfo.CurrentCulture, out configuredValue))
        {
            return defaultValue;
        }

        return Math.Clamp(configuredValue, minimum, maximum);
    }

    private sealed class NullServiceScopeChangeRequestRepository : IServiceScopeChangeRequestRepository
    {
        public static readonly NullServiceScopeChangeRequestRepository Instance = new();

        public Task<IReadOnlyList<ServiceScopeChangeRequest>> GetByAppointmentIdAsync(Guid appointmentId)
        {
            return Task.FromResult<IReadOnlyList<ServiceScopeChangeRequest>>(Array.Empty<ServiceScopeChangeRequest>());
        }

        public Task<IReadOnlyList<ServiceScopeChangeRequest>> GetByServiceRequestIdAsync(Guid serviceRequestId)
        {
            return Task.FromResult<IReadOnlyList<ServiceScopeChangeRequest>>(Array.Empty<ServiceScopeChangeRequest>());
        }

        public Task<IReadOnlyList<ServiceScopeChangeRequest>> GetExpiredPendingByRequestedAtAsync(DateTime requestedAtUtcThreshold, int take = 200)
        {
            return Task.FromResult<IReadOnlyList<ServiceScopeChangeRequest>>(Array.Empty<ServiceScopeChangeRequest>());
        }

        public Task<ServiceScopeChangeRequest?> GetLatestByAppointmentIdAsync(Guid appointmentId)
        {
            return Task.FromResult<ServiceScopeChangeRequest?>(null);
        }

        public Task<ServiceScopeChangeRequest?> GetByIdAsync(Guid scopeChangeRequestId)
        {
            return Task.FromResult<ServiceScopeChangeRequest?>(null);
        }

        public Task<ServiceScopeChangeRequest?> GetByIdWithAttachmentsAsync(Guid scopeChangeRequestId)
        {
            return Task.FromResult<ServiceScopeChangeRequest?>(null);
        }

        public Task<ServiceScopeChangeRequest?> GetLatestByAppointmentIdAndStatusAsync(
            Guid appointmentId,
            ServiceScopeChangeRequestStatus status)
        {
            return Task.FromResult<ServiceScopeChangeRequest?>(null);
        }

        public Task AddAsync(ServiceScopeChangeRequest scopeChangeRequest)
        {
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ServiceScopeChangeRequest scopeChangeRequest)
        {
            return Task.CompletedTask;
        }

        public Task AddAttachmentAsync(ServiceScopeChangeRequestAttachment attachment)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class NullServiceRequestCommercialValueService : IServiceRequestCommercialValueService
    {
        public static readonly NullServiceRequestCommercialValueService Instance = new();

        public Task<ServiceRequestCommercialTotalsDto> RecalculateAsync(ServiceRequest serviceRequest)
        {
            var baseValue = decimal.Round(
                Math.Max(0m, serviceRequest.CommercialBaseValue ?? 0m),
                2,
                MidpointRounding.AwayFromZero);
            var currentValue = decimal.Round(
                Math.Max(baseValue, serviceRequest.CommercialCurrentValue ?? baseValue),
                2,
                MidpointRounding.AwayFromZero);
            var approvedIncrementalValue = decimal.Round(
                Math.Max(0m, currentValue - baseValue),
                2,
                MidpointRounding.AwayFromZero);

            return Task.FromResult(new ServiceRequestCommercialTotalsDto(
                baseValue,
                approvedIncrementalValue,
                currentValue));
        }
    }

    private sealed class NullAppointmentReminderService : IAppointmentReminderService
    {
        public static readonly NullAppointmentReminderService Instance = new();

        public Task ScheduleForAppointmentAsync(Guid appointmentId, string triggerReason) => Task.CompletedTask;
        public Task CancelPendingForAppointmentAsync(Guid appointmentId, string reason) => Task.CompletedTask;
        public Task<int> RegisterPresenceResponseTelemetryAsync(
            Guid appointmentId,
            Guid recipientUserId,
            bool confirmed,
            string? reason,
            DateTime respondedAtUtc) => Task.FromResult(0);
        public Task<int> ProcessDueRemindersAsync(int batchSize = 200, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<AppointmentReminderDispatchListResultDto> GetDispatchesAsync(AppointmentReminderDispatchQueryDto query) =>
            Task.FromResult(new AppointmentReminderDispatchListResultDto(Array.Empty<AppointmentReminderDispatchDto>(), 0, 1, query.PageSize <= 0 ? 50 : query.PageSize));
    }

    private sealed class NullAppointmentChecklistService : IServiceAppointmentChecklistService
    {
        public static readonly NullAppointmentChecklistService Instance = new();

        public Task<ServiceAppointmentChecklistResultDto> GetChecklistAsync(Guid actorUserId, string actorRole, Guid appointmentId)
        {
            return Task.FromResult(new ServiceAppointmentChecklistResultDto(
                true,
                new ServiceAppointmentChecklistDto(
                    appointmentId,
                    null,
                    null,
                    "Checklist nao configurado",
                    false,
                    0,
                    0,
                    Array.Empty<ServiceChecklistItemDto>(),
                    Array.Empty<ServiceChecklistHistoryDto>())));
        }

        public Task<ServiceAppointmentChecklistResultDto> UpsertItemResponseAsync(
            Guid actorUserId,
            string actorRole,
            Guid appointmentId,
            UpsertServiceChecklistItemResponseRequestDto request)
        {
            return Task.FromResult(new ServiceAppointmentChecklistResultDto(
                false,
                ErrorCode: "checklist_not_available",
                ErrorMessage: "Checklist nao disponivel."));
        }

        public Task<ServiceAppointmentChecklistValidationResultDto> ValidateRequiredItemsForCompletionAsync(
            Guid appointmentId,
            string? actorRole = null)
        {
            return Task.FromResult(new ServiceAppointmentChecklistValidationResultDto(
                Success: true,
                CanComplete: true,
                PendingRequiredCount: 0,
                PendingRequiredItems: Array.Empty<string>()));
        }
    }

    private sealed class NullServiceCompletionTermRepository : IServiceCompletionTermRepository
    {
        public static readonly NullServiceCompletionTermRepository Instance = new();

        public Task AddAsync(ServiceCompletionTerm term) => Task.CompletedTask;

        public Task UpdateAsync(ServiceCompletionTerm term) => Task.CompletedTask;

        public Task<ServiceCompletionTerm?> GetByIdAsync(Guid id) => Task.FromResult<ServiceCompletionTerm?>(null);

        public Task<ServiceCompletionTerm?> GetByAppointmentIdAsync(Guid appointmentId) => Task.FromResult<ServiceCompletionTerm?>(null);

        public Task<IReadOnlyList<ServiceCompletionTerm>> GetByRequestIdAsync(Guid requestId) =>
            Task.FromResult<IReadOnlyList<ServiceCompletionTerm>>(Array.Empty<ServiceCompletionTerm>());
    }
}
