using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public sealed class TelegramChatbotSchedulingService : ITelegramChatbotSchedulingService
{
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IProposalRepository _proposalRepository;
    private readonly IServiceAppointmentRepository _serviceAppointmentRepository;
    private readonly IServiceAppointmentService _serviceAppointmentService;

    public TelegramChatbotSchedulingService(
        IServiceRequestRepository serviceRequestRepository,
        IUserRepository userRepository,
        IProposalRepository proposalRepository,
        IServiceAppointmentRepository serviceAppointmentRepository,
        IServiceAppointmentService serviceAppointmentService)
    {
        _serviceRequestRepository = serviceRequestRepository;
        _userRepository = userRepository;
        _proposalRepository = proposalRepository;
        _serviceAppointmentRepository = serviceAppointmentRepository;
        _serviceAppointmentService = serviceAppointmentService;
    }

    public async Task<TelegramChatbotEligibleProvidersResultDto> GetEligibleProvidersAsync(
        Guid clientId,
        Guid serviceRequestId,
        int take = 5)
    {
        if (serviceRequestId == Guid.Empty)
        {
            return new TelegramChatbotEligibleProvidersResultDto(
                Success: false,
                ServiceRequestId: serviceRequestId,
                Providers: [],
                ErrorCode: "invalid_request",
                ErrorMessage: "Pedido invalido para busca de prestadores.");
        }

        var serviceRequest = await _serviceRequestRepository.GetByIdAsync(serviceRequestId);
        if (serviceRequest == null)
        {
            return new TelegramChatbotEligibleProvidersResultDto(
                Success: false,
                ServiceRequestId: serviceRequestId,
                Providers: [],
                ErrorCode: "request_not_found",
                ErrorMessage: "Pedido nao encontrado.");
        }

        if (serviceRequest.ClientId != clientId)
        {
            return new TelegramChatbotEligibleProvidersResultDto(
                Success: false,
                ServiceRequestId: serviceRequestId,
                Providers: [],
                ErrorCode: "forbidden",
                ErrorMessage: "Cliente sem acesso ao pedido informado.");
        }

        if (serviceRequest.Status is ServiceRequestStatus.Canceled or ServiceRequestStatus.Completed)
        {
            return new TelegramChatbotEligibleProvidersResultDto(
                Success: false,
                ServiceRequestId: serviceRequestId,
                Providers: [],
                ErrorCode: "request_closed",
                ErrorMessage: "Pedido encerrado para matching.");
        }

        var safeTake = Math.Clamp(take, 1, 10);
        var providers = await _userRepository.GetAllAsync();
        var clientProfileType = serviceRequest.Client?.ClientProfileType ?? ClientProfileType.Pf;

        var eligible = providers
            .Where(IsEligibleProvider)
            .Select(provider => BuildCandidate(provider, serviceRequest, clientProfileType))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .OrderBy(candidate => candidate.DistanceKm)
            .ThenByDescending(candidate => candidate.ProviderProfile.Rating)
            .Take(safeTake)
            .Select(candidate => new TelegramChatbotEligibleProviderDto(
                ProviderId: candidate.Provider.Id,
                ProviderName: string.IsNullOrWhiteSpace(candidate.Provider.Name)
                    ? "Prestador"
                    : candidate.Provider.Name.Trim(),
                DistanceKm: Math.Round(candidate.DistanceKm, 2, MidpointRounding.AwayFromZero),
                Rating: Math.Round(candidate.ProviderProfile.Rating, 2, MidpointRounding.AwayFromZero),
                ReviewCount: Math.Max(0, candidate.ProviderProfile.ReviewCount),
                CoverageRadiusKm: Math.Round(candidate.ProviderProfile.RadiusKm, 2, MidpointRounding.AwayFromZero),
                BaseZipCode: candidate.ProviderProfile.BaseZipCode,
                Categories: candidate.ProviderProfile.Categories))
            .ToList();

        return new TelegramChatbotEligibleProvidersResultDto(
            Success: true,
            ServiceRequestId: serviceRequestId,
            Providers: eligible);
    }

    public async Task<TelegramChatbotOrdersResultDto> GetClientOrdersAsync(
        Guid clientId,
        int skip = 0,
        int take = 5)
    {
        if (clientId == Guid.Empty)
        {
            return new TelegramChatbotOrdersResultDto(
                Success: false,
                Orders: [],
                TotalCount: 0,
                Skip: 0,
                Take: 0,
                HasMore: false,
                ErrorCode: "invalid_client",
                ErrorMessage: "Cliente invalido para consulta de pedidos.");
        }

        var normalizedSkip = Math.Max(0, skip);
        var normalizedTake = Math.Clamp(take, 1, 20);

        var allOrders = (await _serviceRequestRepository.GetByClientIdAsync(clientId)).ToList();
        var totalCount = allOrders.Count;
        if (totalCount == 0 || normalizedSkip >= totalCount)
        {
            return new TelegramChatbotOrdersResultDto(
                Success: true,
                Orders: [],
                TotalCount: totalCount,
                Skip: normalizedSkip,
                Take: normalizedTake,
                HasMore: false);
        }

        var allAppointments = await _serviceAppointmentRepository.GetByClientAsync(clientId);
        var appointmentsByRequest = allAppointments
            .GroupBy(item => item.ServiceRequestId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ServiceAppointment>)group.ToList());

        var page = allOrders
            .Skip(normalizedSkip)
            .Take(normalizedTake)
            .Select(order =>
            {
                var orderAppointments = appointmentsByRequest.TryGetValue(order.Id, out var grouped)
                    ? grouped
                    : [];

                return BuildOrderSummary(order, orderAppointments);
            })
            .ToList();

        return new TelegramChatbotOrdersResultDto(
            Success: true,
            Orders: page,
            TotalCount: totalCount,
            Skip: normalizedSkip,
            Take: normalizedTake,
            HasMore: normalizedSkip + page.Count < totalCount);
    }

    public async Task<TelegramChatbotOrderStatusResultDto> GetOrderStatusAsync(
        Guid clientId,
        Guid serviceRequestId)
    {
        if (clientId == Guid.Empty || serviceRequestId == Guid.Empty)
        {
            return new TelegramChatbotOrderStatusResultDto(
                Success: false,
                ServiceRequestId: serviceRequestId,
                Protocol: BuildProtocol(serviceRequestId),
                Status: string.Empty,
                ProposalsCount: 0,
                AcceptedProposalsCount: 0,
                AppointmentsCount: 0,
                ErrorCode: "invalid_request",
                ErrorMessage: "Parametros invalidos para consulta de status.");
        }

        var request = await _serviceRequestRepository.GetByIdAsync(serviceRequestId);
        if (request == null)
        {
            return new TelegramChatbotOrderStatusResultDto(
                Success: false,
                ServiceRequestId: serviceRequestId,
                Protocol: BuildProtocol(serviceRequestId),
                Status: string.Empty,
                ProposalsCount: 0,
                AcceptedProposalsCount: 0,
                AppointmentsCount: 0,
                ErrorCode: "request_not_found",
                ErrorMessage: "Pedido nao encontrado.");
        }

        if (request.ClientId != clientId)
        {
            return new TelegramChatbotOrderStatusResultDto(
                Success: false,
                ServiceRequestId: serviceRequestId,
                Protocol: BuildProtocol(serviceRequestId),
                Status: string.Empty,
                ProposalsCount: 0,
                AcceptedProposalsCount: 0,
                AppointmentsCount: 0,
                ErrorCode: "forbidden",
                ErrorMessage: "Cliente sem acesso ao pedido informado.");
        }

        var orderedAppointments = request.Appointments
            .OrderBy(item => NormalizeToUtc(item.WindowStartUtc))
            .ToList();

        var nextAppointment = ResolveNextAppointment(orderedAppointments);

        return new TelegramChatbotOrderStatusResultDto(
            Success: true,
            ServiceRequestId: request.Id,
            Protocol: BuildProtocol(request.Id),
            Status: request.Status.ToString(),
            ProposalsCount: request.Proposals.Count(item => !item.IsInvalidated),
            AcceptedProposalsCount: request.Proposals.Count(item => !item.IsInvalidated && item.Accepted),
            AppointmentsCount: orderedAppointments.Count,
            NextAppointment: nextAppointment is null
                ? null
                : BuildOrderAppointment(nextAppointment));
    }

    public async Task<TelegramChatbotOrderDetailsResultDto> GetOrderDetailsAsync(
        Guid clientId,
        Guid serviceRequestId)
    {
        if (clientId == Guid.Empty || serviceRequestId == Guid.Empty)
        {
            return new TelegramChatbotOrderDetailsResultDto(
                Success: false,
                ServiceRequestId: serviceRequestId,
                ErrorCode: "invalid_request",
                ErrorMessage: "Parametros invalidos para consulta de detalhes.");
        }

        var request = await _serviceRequestRepository.GetByIdAsync(serviceRequestId);
        if (request == null)
        {
            return new TelegramChatbotOrderDetailsResultDto(
                Success: false,
                ServiceRequestId: serviceRequestId,
                ErrorCode: "request_not_found",
                ErrorMessage: "Pedido nao encontrado.");
        }

        if (request.ClientId != clientId)
        {
            return new TelegramChatbotOrderDetailsResultDto(
                Success: false,
                ServiceRequestId: serviceRequestId,
                ErrorCode: "forbidden",
                ErrorMessage: "Cliente sem acesso ao pedido informado.");
        }

        var proposals = request.Proposals
            .Where(item => !item.IsInvalidated)
            .OrderByDescending(item => item.Accepted)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => new TelegramChatbotOrderProposalDto(
                ProposalId: item.Id,
                ProviderId: item.ProviderId,
                ProviderName: ResolveProviderName(item.Provider?.Name),
                EstimatedValue: item.EstimatedValue,
                Accepted: item.Accepted,
                CreatedAtUtc: NormalizeToUtc(item.CreatedAt)))
            .ToList();

        var appointments = request.Appointments
            .OrderByDescending(item => NormalizeToUtc(item.WindowStartUtc))
            .Select(BuildOrderAppointment)
            .ToList();

        var details = new TelegramChatbotOrderDetailsDto(
            ServiceRequestId: request.Id,
            Protocol: BuildProtocol(request.Id),
            Status: request.Status.ToString(),
            Category: ResolveCategoryDisplay(request),
            Description: request.Description,
            Street: request.AddressStreet,
            City: request.AddressCity,
            Zip: request.AddressZip,
            CreatedAtUtc: NormalizeToUtc(request.CreatedAt),
            Proposals: proposals,
            Appointments: appointments);

        return new TelegramChatbotOrderDetailsResultDto(
            Success: true,
            ServiceRequestId: request.Id,
            Details: details);
    }

    public async Task<TelegramChatbotAppointmentsResultDto> GetClientAppointmentsAsync(
        Guid clientId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int skip = 0,
        int take = 5)
    {
        if (clientId == Guid.Empty)
        {
            return new TelegramChatbotAppointmentsResultDto(
                Success: false,
                Appointments: [],
                TotalCount: 0,
                Skip: 0,
                Take: 0,
                HasMore: false,
                ErrorCode: "invalid_client",
                ErrorMessage: "Cliente invalido para consulta de agendamentos.");
        }

        var normalizedSkip = Math.Max(0, skip);
        var normalizedTake = Math.Clamp(take, 1, 20);
        DateTime? normalizedFromUtc = fromUtc.HasValue ? NormalizeToUtc(fromUtc.Value) : null;
        DateTime? normalizedToUtc = toUtc.HasValue ? NormalizeToUtc(toUtc.Value) : null;

        var allAppointments = await _serviceAppointmentRepository.GetByClientAsync(
            clientId,
            normalizedFromUtc,
            normalizedToUtc);

        var totalCount = allAppointments.Count;
        if (totalCount == 0 || normalizedSkip >= totalCount)
        {
            return new TelegramChatbotAppointmentsResultDto(
                Success: true,
                Appointments: [],
                TotalCount: totalCount,
                Skip: normalizedSkip,
                Take: normalizedTake,
                HasMore: false);
        }

        var page = allAppointments
            .Skip(normalizedSkip)
            .Take(normalizedTake)
            .Select(BuildAppointmentSummary)
            .ToList();

        return new TelegramChatbotAppointmentsResultDto(
            Success: true,
            Appointments: page,
            TotalCount: totalCount,
            Skip: normalizedSkip,
            Take: normalizedTake,
            HasMore: normalizedSkip + page.Count < totalCount);
    }

    public async Task<TelegramChatbotBatchScheduleResultDto> ScheduleVisitsAsync(
        TelegramChatbotBatchScheduleRequestDto request)
    {
        if (request.ClientId == Guid.Empty || request.ServiceRequestId == Guid.Empty)
        {
            return new TelegramChatbotBatchScheduleResultDto(
                Success: false,
                ServiceRequestId: request.ServiceRequestId,
                Results: [],
                ErrorCode: "invalid_request",
                ErrorMessage: "Dados invalidos para agendamento em lote.");
        }

        if (request.Visits == null || request.Visits.Count == 0)
        {
            return new TelegramChatbotBatchScheduleResultDto(
                Success: false,
                ServiceRequestId: request.ServiceRequestId,
                Results: [],
                ErrorCode: "empty_visits",
                ErrorMessage: "Informe ao menos uma visita para agendar.");
        }

        if (request.Visits.Count > 3)
        {
            return new TelegramChatbotBatchScheduleResultDto(
                Success: false,
                ServiceRequestId: request.ServiceRequestId,
                Results: [],
                ErrorCode: "max_visits_exceeded",
                ErrorMessage: "O limite e de ate 3 visitas por solicitacao.");
        }

        var serviceRequest = await _serviceRequestRepository.GetByIdAsync(request.ServiceRequestId);
        if (serviceRequest == null)
        {
            return new TelegramChatbotBatchScheduleResultDto(
                Success: false,
                ServiceRequestId: request.ServiceRequestId,
                Results: [],
                ErrorCode: "request_not_found",
                ErrorMessage: "Pedido nao encontrado.");
        }

        if (serviceRequest.ClientId != request.ClientId)
        {
            return new TelegramChatbotBatchScheduleResultDto(
                Success: false,
                ServiceRequestId: request.ServiceRequestId,
                Results: [],
                ErrorCode: "forbidden",
                ErrorMessage: "Cliente sem acesso ao pedido informado.");
        }

        if (serviceRequest.Status is ServiceRequestStatus.Canceled or ServiceRequestStatus.Completed)
        {
            return new TelegramChatbotBatchScheduleResultDto(
                Success: false,
                ServiceRequestId: request.ServiceRequestId,
                Results: [],
                ErrorCode: "request_closed",
                ErrorMessage: "Pedido encerrado para novos agendamentos.");
        }

        var duplicateDay = TryGetDuplicateVisitDay(request.Visits);
        if (duplicateDay.HasValue)
        {
            return new TelegramChatbotBatchScheduleResultDto(
                Success: false,
                ServiceRequestId: request.ServiceRequestId,
                Results: [],
                ErrorCode: "duplicate_visit_day",
                ErrorMessage: $"As visitas devem ocorrer em dias distintos. Dia duplicado: {duplicateDay:yyyy-MM-dd}.");
        }

        var providerMap = await BuildEligibleProviderMapAsync(serviceRequest);
        var results = new List<TelegramChatbotBatchScheduleVisitResultDto>(request.Visits.Count);
        var successCount = 0;

        foreach (var visit in request.Visits)
        {
            if (visit.ProviderId == Guid.Empty)
            {
                results.Add(new TelegramChatbotBatchScheduleVisitResultDto(
                    ProviderId: visit.ProviderId,
                    WindowStartUtc: visit.WindowStartUtc,
                    WindowEndUtc: visit.WindowEndUtc,
                    Success: false,
                    ErrorCode: "invalid_provider",
                    ErrorMessage: "Prestador invalido para agendamento."));
                continue;
            }

            var windowStartUtc = NormalizeToUtc(visit.WindowStartUtc);
            var windowEndUtc = NormalizeToUtc(visit.WindowEndUtc);

            if (windowEndUtc <= windowStartUtc)
            {
                results.Add(new TelegramChatbotBatchScheduleVisitResultDto(
                    ProviderId: visit.ProviderId,
                    WindowStartUtc: windowStartUtc,
                    WindowEndUtc: windowEndUtc,
                    Success: false,
                    ErrorCode: "invalid_window",
                    ErrorMessage: "Janela de horario invalida."));
                continue;
            }

            if (!providerMap.ContainsKey(visit.ProviderId))
            {
                results.Add(new TelegramChatbotBatchScheduleVisitResultDto(
                    ProviderId: visit.ProviderId,
                    WindowStartUtc: windowStartUtc,
                    WindowEndUtc: windowEndUtc,
                    Success: false,
                    ErrorCode: "provider_not_eligible",
                    ErrorMessage: "Prestador nao elegivel para o pedido informado."));
                continue;
            }

            await EnsureAcceptedProposalForProviderAsync(serviceRequest, visit.ProviderId);

            var createResult = await _serviceAppointmentService.CreateAsync(
                request.ClientId,
                "Client",
                new CreateServiceAppointmentRequestDto(
                    ServiceRequestId: request.ServiceRequestId,
                    ProviderId: visit.ProviderId,
                    WindowStartUtc: windowStartUtc,
                    WindowEndUtc: windowEndUtc,
                    Reason: string.IsNullOrWhiteSpace(visit.Reason)
                        ? "Agendamento solicitado pelo chatbot Telegram."
                        : visit.Reason.Trim()));

            if (createResult.Success && createResult.Appointment != null)
            {
                successCount++;
                results.Add(new TelegramChatbotBatchScheduleVisitResultDto(
                    ProviderId: visit.ProviderId,
                    WindowStartUtc: windowStartUtc,
                    WindowEndUtc: windowEndUtc,
                    Success: true,
                    AppointmentId: createResult.Appointment.Id));
                continue;
            }

            results.Add(new TelegramChatbotBatchScheduleVisitResultDto(
                ProviderId: visit.ProviderId,
                WindowStartUtc: windowStartUtc,
                WindowEndUtc: windowEndUtc,
                Success: false,
                ErrorCode: createResult.ErrorCode ?? "schedule_failed",
                ErrorMessage: createResult.ErrorMessage ?? "Nao foi possivel criar o agendamento."));
        }

        return new TelegramChatbotBatchScheduleResultDto(
            Success: successCount == request.Visits.Count,
            ServiceRequestId: request.ServiceRequestId,
            Results: results,
            ErrorCode: successCount == request.Visits.Count ? null : "batch_partial_or_failed",
            ErrorMessage: successCount == request.Visits.Count
                ? null
                : "Uma ou mais visitas nao puderam ser agendadas.");
    }

    private static TelegramChatbotOrderSummaryDto BuildOrderSummary(
        ServiceRequest request,
        IReadOnlyList<ServiceAppointment> appointments)
    {
        var nextAppointment = ResolveNextAppointment(appointments);

        return new TelegramChatbotOrderSummaryDto(
            ServiceRequestId: request.Id,
            Protocol: BuildProtocol(request.Id),
            Status: request.Status.ToString(),
            Category: ResolveCategoryDisplay(request),
            Description: request.Description,
            City: request.AddressCity,
            CreatedAtUtc: NormalizeToUtc(request.CreatedAt),
            ProposalsCount: request.Proposals.Count(item => !item.IsInvalidated),
            AcceptedProposalsCount: request.Proposals.Count(item => !item.IsInvalidated && item.Accepted),
            AppointmentsCount: appointments.Count,
            NextAppointmentStartUtc: nextAppointment is null
                ? null
                : NormalizeToUtc(nextAppointment.WindowStartUtc),
            NextAppointmentEndUtc: nextAppointment is null
                ? null
                : NormalizeToUtc(nextAppointment.WindowEndUtc),
            NextAppointmentStatus: nextAppointment?.Status.ToString());
    }

    private static TelegramChatbotAppointmentSummaryDto BuildAppointmentSummary(ServiceAppointment appointment)
    {
        return new TelegramChatbotAppointmentSummaryDto(
            AppointmentId: appointment.Id,
            ServiceRequestId: appointment.ServiceRequestId,
            Protocol: BuildProtocol(appointment.ServiceRequestId),
            ProviderId: appointment.ProviderId,
            ProviderName: ResolveProviderName(appointment.Provider?.Name),
            Status: appointment.Status.ToString(),
            WindowStartUtc: NormalizeToUtc(appointment.WindowStartUtc),
            WindowEndUtc: NormalizeToUtc(appointment.WindowEndUtc),
            Reason: appointment.Reason);
    }

    private static TelegramChatbotOrderAppointmentDto BuildOrderAppointment(ServiceAppointment appointment)
    {
        return new TelegramChatbotOrderAppointmentDto(
            AppointmentId: appointment.Id,
            ProviderId: appointment.ProviderId,
            ProviderName: ResolveProviderName(appointment.Provider?.Name),
            Status: appointment.Status.ToString(),
            WindowStartUtc: NormalizeToUtc(appointment.WindowStartUtc),
            WindowEndUtc: NormalizeToUtc(appointment.WindowEndUtc));
    }

    private static ServiceAppointment? ResolveNextAppointment(IEnumerable<ServiceAppointment> appointments)
    {
        var nowUtc = DateTime.UtcNow;
        return appointments
                   .Where(item => NormalizeToUtc(item.WindowEndUtc) >= nowUtc)
                   .OrderBy(item => NormalizeToUtc(item.WindowStartUtc))
                   .FirstOrDefault()
               ?? appointments
                   .OrderByDescending(item => NormalizeToUtc(item.WindowStartUtc))
                   .FirstOrDefault();
    }

    private static string ResolveCategoryDisplay(ServiceRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.CategoryDefinition?.Name))
        {
            return request.CategoryDefinition.Name;
        }

        return request.Category.ToPtBr();
    }

    private static string ResolveProviderName(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Prestador"
            : value.Trim();
    }

    private static string BuildProtocol(Guid serviceRequestId)
    {
        if (serviceRequestId == Guid.Empty)
        {
            return string.Empty;
        }

        return serviceRequestId.ToString("N")[..8];
    }

    private static bool IsEligibleProvider(User provider)
    {
        if (provider.Role != UserRole.Provider || !provider.IsActive)
        {
            return false;
        }

        var profile = provider.ProviderProfile;
        if (profile == null || !profile.BaseLatitude.HasValue || !profile.BaseLongitude.HasValue)
        {
            return false;
        }

        if (profile.RadiusKm <= 0)
        {
            return false;
        }

        if (profile.Categories == null || profile.Categories.Count == 0)
        {
            return false;
        }

        return true;
    }

    private static EligibleProviderCandidate? BuildCandidate(
        User provider,
        ServiceRequest serviceRequest,
        ClientProfileType clientProfileType)
    {
        var profile = provider.ProviderProfile;
        if (profile == null || !profile.BaseLatitude.HasValue || !profile.BaseLongitude.HasValue)
        {
            return null;
        }

        if (!profile.Categories.Contains(serviceRequest.Category))
        {
            return null;
        }

        if (!CanProviderAttendClientType(profile.ClientPreference, clientProfileType))
        {
            return null;
        }

        var distanceKm = CalculateDistanceKm(
            profile.BaseLatitude.Value,
            profile.BaseLongitude.Value,
            serviceRequest.Latitude,
            serviceRequest.Longitude);

        if (distanceKm > profile.RadiusKm)
        {
            return null;
        }

        return new EligibleProviderCandidate(provider, profile, distanceKm);
    }

    private static bool CanProviderAttendClientType(ProviderClientPreference preference, ClientProfileType clientProfileType)
    {
        return preference switch
        {
            ProviderClientPreference.PfOnly => clientProfileType == ClientProfileType.Pf,
            ProviderClientPreference.PjOnly => clientProfileType == ClientProfileType.Pj,
            _ => true
        };
    }

    private static double CalculateDistanceKm(double fromLat, double fromLng, double toLat, double toLng)
    {
        const double earthRadiusKm = 6371.0;

        var dLat = DegreesToRadians(toLat - fromLat);
        var dLng = DegreesToRadians(toLng - fromLng);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(fromLat)) * Math.Cos(DegreesToRadians(toLat)) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    private async Task<Dictionary<Guid, EligibleProviderCandidate>> BuildEligibleProviderMapAsync(ServiceRequest serviceRequest)
    {
        var providers = await _userRepository.GetAllAsync();
        var clientProfileType = serviceRequest.Client?.ClientProfileType ?? ClientProfileType.Pf;

        return providers
            .Where(IsEligibleProvider)
            .Select(provider => BuildCandidate(provider, serviceRequest, clientProfileType))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .ToDictionary(item => item.Provider.Id, item => item);
    }

    private async Task EnsureAcceptedProposalForProviderAsync(ServiceRequest serviceRequest, Guid providerId)
    {
        var existingProposal = serviceRequest.Proposals
            .Where(proposal => proposal.ProviderId == providerId && !proposal.IsInvalidated)
            .OrderByDescending(proposal => proposal.CreatedAt)
            .FirstOrDefault();

        if (existingProposal == null)
        {
            var proposal = new Proposal
            {
                RequestId = serviceRequest.Id,
                ProviderId = providerId,
                Accepted = true,
                IsInvalidated = false,
                Message = "Proposta gerada automaticamente para agendamento via chatbot Telegram."
            };

            await _proposalRepository.AddAsync(proposal);
            serviceRequest.Proposals.Add(proposal);
            return;
        }

        if (!existingProposal.Accepted)
        {
            existingProposal.Accepted = true;
            await _proposalRepository.UpdateAsync(existingProposal);
        }
    }

    private static DateOnly? TryGetDuplicateVisitDay(IReadOnlyList<TelegramChatbotBatchScheduleVisitRequestDto> visits)
    {
        var seenDays = new HashSet<DateOnly>();
        foreach (var visit in visits)
        {
            var day = DateOnly.FromDateTime(NormalizeToUtc(visit.WindowStartUtc));
            if (!seenDays.Add(day))
            {
                return day;
            }
        }

        return null;
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

    private sealed record EligibleProviderCandidate(
        User Provider,
        ProviderProfile ProviderProfile,
        double DistanceKm);
}
