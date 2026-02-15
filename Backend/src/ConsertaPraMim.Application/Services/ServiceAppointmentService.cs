using System.Collections.Concurrent;
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
    private readonly IServiceAppointmentChecklistService _serviceAppointmentChecklistService;
    private readonly IAppointmentReminderService _appointmentReminderService;
    private readonly TimeZoneInfo _availabilityTimeZone;
    private readonly int _providerConfirmationExpiryHours;
    private readonly int _cancelMinimumHoursBeforeWindow;
    private readonly int _rescheduleMinimumHoursBeforeWindow;
    private readonly int _rescheduleMaximumAdvanceDays;

    public ServiceAppointmentService(
        IServiceAppointmentRepository serviceAppointmentRepository,
        IServiceRequestRepository serviceRequestRepository,
        IUserRepository userRepository,
        INotificationService notificationService,
        IConfiguration configuration,
        IAppointmentReminderService? appointmentReminderService = null,
        IServiceAppointmentChecklistService? serviceAppointmentChecklistService = null)
    {
        _serviceAppointmentRepository = serviceAppointmentRepository;
        _serviceRequestRepository = serviceRequestRepository;
        _userRepository = userRepository;
        _notificationService = notificationService;
        _appointmentReminderService = appointmentReminderService ?? NullAppointmentReminderService.Instance;
        _serviceAppointmentChecklistService = serviceAppointmentChecklistService ?? NullAppointmentChecklistService.Instance;
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
            operationLock.Release();
        }
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

    private static bool IsServiceRequestClosedForScheduling(ServiceRequestStatus status)
    {
        return status is
            ServiceRequestStatus.Canceled or
            ServiceRequestStatus.Completed or
            ServiceRequestStatus.Validated or
            ServiceRequestStatus.PendingClientCompletionAcceptance;
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
            appointment.OperationalStatusReason);
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

    private sealed class NullAppointmentReminderService : IAppointmentReminderService
    {
        public static readonly NullAppointmentReminderService Instance = new();

        public Task ScheduleForAppointmentAsync(Guid appointmentId, string triggerReason) => Task.CompletedTask;
        public Task CancelPendingForAppointmentAsync(Guid appointmentId, string reason) => Task.CompletedTask;
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
}
