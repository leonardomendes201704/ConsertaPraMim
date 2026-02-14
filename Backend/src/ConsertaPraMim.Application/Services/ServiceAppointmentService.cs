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

    private static readonly IReadOnlyCollection<ServiceAppointmentStatus> BlockingStatuses = new[]
    {
        ServiceAppointmentStatus.PendingProviderConfirmation,
        ServiceAppointmentStatus.Confirmed,
        ServiceAppointmentStatus.RescheduleRequestedByClient,
        ServiceAppointmentStatus.RescheduleRequestedByProvider,
        ServiceAppointmentStatus.RescheduleConfirmed
    };

    private readonly IServiceAppointmentRepository _serviceAppointmentRepository;
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly int _providerConfirmationExpiryHours;
    private readonly int _cancelMinimumHoursBeforeWindow;
    private readonly int _rescheduleMinimumHoursBeforeWindow;
    private readonly int _rescheduleMaximumAdvanceDays;

    public ServiceAppointmentService(
        IServiceAppointmentRepository serviceAppointmentRepository,
        IServiceRequestRepository serviceRequestRepository,
        IUserRepository userRepository,
        INotificationService notificationService,
        IConfiguration configuration)
    {
        _serviceAppointmentRepository = serviceAppointmentRepository;
        _serviceRequestRepository = serviceRequestRepository;
        _userRepository = userRepository;
        _notificationService = notificationService;

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

        if (serviceRequest.Status is ServiceRequestStatus.Canceled or ServiceRequestStatus.Completed or ServiceRequestStatus.Validated)
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

        var lockKey = $"{request.ProviderId:N}:{windowStartUtc:yyyyMMdd}";
        var lockInstance = AppointmentCreationLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));

        await lockInstance.WaitAsync();
        try
        {
            var existingAppointment = await _serviceAppointmentRepository.GetByRequestIdAsync(request.ServiceRequestId);
            if (existingAppointment != null)
            {
                return new ServiceAppointmentOperationResultDto(
                    false,
                    ErrorCode: "appointment_already_exists",
                    ErrorMessage: "Este pedido ja possui agendamento.");
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

            if (serviceRequest.Status != ServiceRequestStatus.Scheduled)
            {
                serviceRequest.Status = ServiceRequestStatus.Scheduled;
                await _serviceRequestRepository.UpdateAsync(serviceRequest);
            }

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
            lockInstance.Release();
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
        appointment.Status = ServiceAppointmentStatus.Confirmed;
        appointment.ConfirmedAtUtc = nowUtc;
        appointment.ExpiresAtUtc = null;
        appointment.UpdatedAt = nowUtc;
        await _serviceAppointmentRepository.UpdateAsync(appointment);

        await _serviceAppointmentRepository.AddHistoryAsync(new ServiceAppointmentHistory
        {
            ServiceAppointmentId = appointment.Id,
            PreviousStatus = previousStatus,
            NewStatus = ServiceAppointmentStatus.Confirmed,
            ActorUserId = actorUserId,
            ActorRole = ResolveActorRole(actorRole),
            Reason = "Agendamento confirmado pelo prestador."
        });

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

        if (appointment.ServiceRequest.Status == ServiceRequestStatus.Scheduled)
        {
            appointment.ServiceRequest.Status = ServiceRequestStatus.Matching;
            await _serviceRequestRepository.UpdateAsync(appointment.ServiceRequest);
        }

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
            appointment.ConfirmedAtUtc ??= nowUtc;
            ClearRescheduleProposal(appointment);
            appointment.UpdatedAt = nowUtc;
            await _serviceAppointmentRepository.UpdateAsync(appointment);

            await _serviceAppointmentRepository.AddHistoryAsync(new ServiceAppointmentHistory
            {
                ServiceAppointmentId = appointment.Id,
                PreviousStatus = previousStatus,
                NewStatus = ServiceAppointmentStatus.RescheduleConfirmed,
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

        if (IsClientRole(actorRole))
        {
            if (appointment.ServiceRequest.Status != ServiceRequestStatus.Canceled &&
                appointment.ServiceRequest.Status != ServiceRequestStatus.Completed &&
                appointment.ServiceRequest.Status != ServiceRequestStatus.Validated)
            {
                appointment.ServiceRequest.Status = ServiceRequestStatus.Canceled;
                await _serviceRequestRepository.UpdateAsync(appointment.ServiceRequest);
            }
        }
        else if (appointment.ServiceRequest.Status == ServiceRequestStatus.Scheduled)
        {
            appointment.ServiceRequest.Status = ServiceRequestStatus.Matching;
            await _serviceRequestRepository.UpdateAsync(appointment.ServiceRequest);
        }

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

        var loaded = await _serviceAppointmentRepository.GetByIdAsync(appointment.Id) ?? appointment;
        return new ServiceAppointmentOperationResultDto(true, MapToDto(loaded));
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

            if (appointment.ServiceRequest.Status == ServiceRequestStatus.Scheduled)
            {
                appointment.ServiceRequest.Status = ServiceRequestStatus.Matching;
                await _serviceRequestRepository.UpdateAsync(appointment.ServiceRequest);
            }

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

        var isInsideAnyRule = rules.Any(r =>
            r.DayOfWeek == windowStartUtc.DayOfWeek &&
            r.StartTime <= windowStartUtc.TimeOfDay &&
            r.EndTime >= windowEndUtc.TimeOfDay);

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

    private static IReadOnlyList<ServiceAppointmentSlotDto> BuildAvailableSlots(
        IReadOnlyList<ProviderAvailabilityRule> rules,
        IReadOnlyList<ProviderAvailabilityException> exceptions,
        IReadOnlyList<ServiceAppointment> reservedAppointments,
        DateTime rangeStartUtc,
        DateTime rangeEndUtc,
        int? requestedSlotDurationMinutes)
    {
        var dedup = new HashSet<string>(StringComparer.Ordinal);
        var slots = new List<ServiceAppointmentSlotDto>();

        for (var day = rangeStartUtc.Date; day <= rangeEndUtc.Date; day = day.AddDays(1))
        {
            foreach (var rule in rules.Where(r => r.DayOfWeek == day.DayOfWeek))
            {
                var slotDurationMinutes = requestedSlotDurationMinutes ?? rule.SlotDurationMinutes;
                if (slotDurationMinutes < MinimumSlotDurationMinutes || slotDurationMinutes > MaximumSlotDurationMinutes)
                {
                    continue;
                }

                var ruleStartUtc = day.Add(rule.StartTime);
                var ruleEndUtc = day.Add(rule.EndTime);

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
                    h.OccurredAtUtc))
                .ToList());
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
}
