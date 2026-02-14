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

        var configuredExpiryHoursRaw = configuration["ServiceAppointments:ConfirmationExpiryHours"];
        _providerConfirmationExpiryHours = int.TryParse(configuredExpiryHoursRaw, out var configuredExpiryHours)
            ? Math.Clamp(configuredExpiryHours, 1, 72)
            : 12;
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

    private async Task<bool> IsSlotAvailableForProviderAsync(Guid providerId, DateTime windowStartUtc, DateTime windowEndUtc)
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

        return !conflictingAppointments.Any(a => Overlaps(windowStartUtc, windowEndUtc, a.WindowStartUtc, a.WindowEndUtc));
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
}
