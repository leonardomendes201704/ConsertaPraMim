using System.Globalization;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;

namespace ConsertaPraMim.Web.Client.Services;

public class ClientApiServiceAppointmentService : IServiceAppointmentService
{
    private readonly ClientApiCaller _apiCaller;

    public ClientApiServiceAppointmentService(ClientApiCaller apiCaller)
    {
        _apiCaller = apiCaller;
    }

    public async Task<ServiceAppointmentSlotsResultDto> GetAvailableSlotsAsync(
        Guid actorUserId,
        string actorRole,
        GetServiceAppointmentSlotsQueryDto query)
    {
        var path =
            "/api/service-appointments/slots" +
            $"?providerId={query.ProviderId}" +
            $"&fromUtc={Uri.EscapeDataString(query.FromUtc.ToString("O", CultureInfo.InvariantCulture))}" +
            $"&toUtc={Uri.EscapeDataString(query.ToUtc.ToString("O", CultureInfo.InvariantCulture))}";

        if (query.SlotDurationMinutes.HasValue)
        {
            path += $"&slotDurationMinutes={query.SlotDurationMinutes.Value}";
        }

        var response = await _apiCaller.SendAsync<List<ServiceAppointmentSlotDto>>(HttpMethod.Get, path);
        if (response.Success)
        {
            return new ServiceAppointmentSlotsResultDto(true, response.Payload ?? []);
        }

        return new ServiceAppointmentSlotsResultDto(false, [], "api_error", response.ErrorMessage);
    }

    public async Task<ProviderAvailabilityOverviewResultDto> GetProviderAvailabilityOverviewAsync(
        Guid actorUserId,
        string actorRole,
        Guid providerId)
    {
        var response = await _apiCaller.SendAsync<ProviderAvailabilityOverviewDto>(HttpMethod.Get, $"/api/service-appointments/providers/{providerId}/availability");
        if (response.Success && response.Payload != null)
        {
            return new ProviderAvailabilityOverviewResultDto(true, response.Payload);
        }

        return new ProviderAvailabilityOverviewResultDto(false, null, "api_error", response.ErrorMessage);
    }

    public async Task<ProviderAvailabilityOperationResultDto> AddProviderAvailabilityRuleAsync(
        Guid actorUserId,
        string actorRole,
        CreateProviderAvailabilityRuleRequestDto request)
    {
        var response = await _apiCaller.SendAsync<ProviderAvailabilityOperationResultDto>(HttpMethod.Post, "/api/service-appointments/availability/rules", request);
        return response.Payload ?? new ProviderAvailabilityOperationResultDto(false, "api_error", response.ErrorMessage);
    }

    public async Task<ProviderAvailabilityOperationResultDto> RemoveProviderAvailabilityRuleAsync(
        Guid actorUserId,
        string actorRole,
        Guid ruleId)
    {
        var response = await _apiCaller.SendAsync<ProviderAvailabilityOperationResultDto>(HttpMethod.Delete, $"/api/service-appointments/availability/rules/{ruleId}");
        return response.Payload ?? new ProviderAvailabilityOperationResultDto(false, "api_error", response.ErrorMessage);
    }

    public async Task<ProviderAvailabilityOperationResultDto> AddProviderAvailabilityExceptionAsync(
        Guid actorUserId,
        string actorRole,
        CreateProviderAvailabilityExceptionRequestDto request)
    {
        var response = await _apiCaller.SendAsync<ProviderAvailabilityOperationResultDto>(HttpMethod.Post, "/api/service-appointments/availability/blocks", request);
        return response.Payload ?? new ProviderAvailabilityOperationResultDto(false, "api_error", response.ErrorMessage);
    }

    public async Task<ProviderAvailabilityOperationResultDto> RemoveProviderAvailabilityExceptionAsync(
        Guid actorUserId,
        string actorRole,
        Guid exceptionId)
    {
        var response = await _apiCaller.SendAsync<ProviderAvailabilityOperationResultDto>(HttpMethod.Delete, $"/api/service-appointments/availability/blocks/{exceptionId}");
        return response.Payload ?? new ProviderAvailabilityOperationResultDto(false, "api_error", response.ErrorMessage);
    }

    public async Task<ServiceAppointmentOperationResultDto> CreateAsync(
        Guid actorUserId,
        string actorRole,
        CreateServiceAppointmentRequestDto request)
    {
        var response = await _apiCaller.SendAsync<ServiceAppointmentOperationResultDto>(HttpMethod.Post, "/api/service-appointments", request);
        return response.Payload ?? new ServiceAppointmentOperationResultDto(false, null, "api_error", response.ErrorMessage);
    }

    public async Task<ServiceAppointmentOperationResultDto> ConfirmAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId)
    {
        var response = await _apiCaller.SendAsync<ServiceAppointmentOperationResultDto>(HttpMethod.Post, $"/api/service-appointments/{appointmentId}/confirm");
        return response.Payload ?? new ServiceAppointmentOperationResultDto(false, null, "api_error", response.ErrorMessage);
    }

    public async Task<ServiceAppointmentOperationResultDto> RejectAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        RejectServiceAppointmentRequestDto request)
    {
        var response = await _apiCaller.SendAsync<ServiceAppointmentOperationResultDto>(HttpMethod.Post, $"/api/service-appointments/{appointmentId}/reject", request);
        return response.Payload ?? new ServiceAppointmentOperationResultDto(false, null, "api_error", response.ErrorMessage);
    }

    public async Task<ServiceAppointmentOperationResultDto> RequestRescheduleAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        RequestServiceAppointmentRescheduleDto request)
    {
        var response = await _apiCaller.SendAsync<ServiceAppointmentOperationResultDto>(HttpMethod.Post, $"/api/service-appointments/{appointmentId}/reschedule", request);
        return response.Payload ?? new ServiceAppointmentOperationResultDto(false, null, "api_error", response.ErrorMessage);
    }

    public async Task<ServiceAppointmentOperationResultDto> RespondRescheduleAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        RespondServiceAppointmentRescheduleRequestDto request)
    {
        var response = await _apiCaller.SendAsync<ServiceAppointmentOperationResultDto>(HttpMethod.Post, $"/api/service-appointments/{appointmentId}/reschedule/respond", request);
        return response.Payload ?? new ServiceAppointmentOperationResultDto(false, null, "api_error", response.ErrorMessage);
    }

    public async Task<ServiceAppointmentOperationResultDto> CancelAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        CancelServiceAppointmentRequestDto request)
    {
        var response = await _apiCaller.SendAsync<ServiceAppointmentOperationResultDto>(HttpMethod.Post, $"/api/service-appointments/{appointmentId}/cancel", request);
        return response.Payload ?? new ServiceAppointmentOperationResultDto(false, null, "api_error", response.ErrorMessage);
    }

    public Task<ServiceAppointmentOperationResultDto> OverrideFinancialPolicyAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        ServiceFinancialPolicyOverrideRequestDto request) =>
        Task.FromResult(new ServiceAppointmentOperationResultDto(false, null, "not_supported", "Operacao nao suportada no portal cliente."));

    public async Task<ServiceAppointmentOperationResultDto> MarkArrivedAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        MarkServiceAppointmentArrivalRequestDto request)
    {
        var response = await _apiCaller.SendAsync<ServiceAppointmentOperationResultDto>(HttpMethod.Post, $"/api/service-appointments/{appointmentId}/arrive", request);
        return response.Payload ?? new ServiceAppointmentOperationResultDto(false, null, "api_error", response.ErrorMessage);
    }

    public async Task<ServiceAppointmentOperationResultDto> StartExecutionAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        StartServiceAppointmentExecutionRequestDto request)
    {
        var response = await _apiCaller.SendAsync<ServiceAppointmentOperationResultDto>(HttpMethod.Post, $"/api/service-appointments/{appointmentId}/start", request);
        return response.Payload ?? new ServiceAppointmentOperationResultDto(false, null, "api_error", response.ErrorMessage);
    }

    public async Task<ServiceAppointmentOperationResultDto> RespondPresenceAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        RespondServiceAppointmentPresenceRequestDto request)
    {
        var response = await _apiCaller.SendAsync<ServiceAppointmentOperationResultDto>(HttpMethod.Post, $"/api/service-appointments/{appointmentId}/presence/respond", request);
        return response.Payload ?? new ServiceAppointmentOperationResultDto(false, null, "api_error", response.ErrorMessage);
    }

    public async Task<ServiceAppointmentOperationResultDto> UpdateOperationalStatusAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        UpdateServiceAppointmentOperationalStatusRequestDto request)
    {
        var response = await _apiCaller.SendAsync<ServiceAppointmentOperationResultDto>(HttpMethod.Post, $"/api/service-appointments/{appointmentId}/operational-status", request);
        return response.Payload ?? new ServiceAppointmentOperationResultDto(false, null, "api_error", response.ErrorMessage);
    }

    public Task<ServiceScopeChangeRequestOperationResultDto> CreateScopeChangeRequestAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        CreateServiceScopeChangeRequestDto request) =>
        Task.FromResult(new ServiceScopeChangeRequestOperationResultDto(false, null, "not_supported", "Operacao nao suportada no portal cliente."));

    public Task<ServiceWarrantyClaimOperationResultDto> CreateWarrantyClaimAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        CreateServiceWarrantyClaimRequestDto request) =>
        Task.FromResult(new ServiceWarrantyClaimOperationResultDto(false, null, "not_supported", "Operacao nao suportada no portal cliente."));

    public Task<ServiceDisputeCaseOperationResultDto> CreateDisputeCaseAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        CreateServiceDisputeCaseRequestDto request) =>
        Task.FromResult(new ServiceDisputeCaseOperationResultDto(false, null, "not_supported", "Operacao nao suportada no portal cliente."));

    public Task<ServiceWarrantyRevisitOperationResultDto> ScheduleWarrantyRevisitAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        Guid warrantyClaimId,
        ScheduleServiceWarrantyRevisitRequestDto request) =>
        Task.FromResult(new ServiceWarrantyRevisitOperationResultDto(false, null, null, "not_supported", "Operacao nao suportada no portal cliente."));

    public Task<ServiceWarrantyClaimOperationResultDto> RespondWarrantyClaimAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        Guid warrantyClaimId,
        RespondServiceWarrantyClaimRequestDto request) =>
        Task.FromResult(new ServiceWarrantyClaimOperationResultDto(false, null, "not_supported", "Operacao nao suportada no portal cliente."));

    public Task<ServiceScopeChangeAttachmentOperationResultDto> AddScopeChangeAttachmentAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        Guid scopeChangeRequestId,
        RegisterServiceScopeChangeAttachmentDto request) =>
        Task.FromResult(new ServiceScopeChangeAttachmentOperationResultDto(false, null, "not_supported", "Operacao nao suportada no portal cliente."));

    public Task<ServiceDisputeCaseAttachmentOperationResultDto> AddDisputeCaseAttachmentAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        Guid disputeCaseId,
        RegisterServiceDisputeAttachmentDto request) =>
        Task.FromResult(new ServiceDisputeCaseAttachmentOperationResultDto(false, null, "not_supported", "Operacao nao suportada no portal cliente."));

    public Task<ServiceDisputeCaseMessageOperationResultDto> AddDisputeCaseMessageAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        Guid disputeCaseId,
        CreateServiceDisputeMessageRequestDto request) =>
        Task.FromResult(new ServiceDisputeCaseMessageOperationResultDto(false, null, "not_supported", "Operacao nao suportada no portal cliente."));

    public async Task<ServiceScopeChangeRequestOperationResultDto> ApproveScopeChangeRequestAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        Guid scopeChangeRequestId)
    {
        var response = await _apiCaller.SendAsync<ServiceScopeChangeRequestOperationResultDto>(
            HttpMethod.Post,
            $"/api/service-appointments/{appointmentId}/scope-changes/{scopeChangeRequestId}/approve");
        return response.Payload ?? new ServiceScopeChangeRequestOperationResultDto(false, null, "api_error", response.ErrorMessage);
    }

    public async Task<ServiceScopeChangeRequestOperationResultDto> RejectScopeChangeRequestAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        Guid scopeChangeRequestId,
        RejectServiceScopeChangeRequestDto request)
    {
        var response = await _apiCaller.SendAsync<ServiceScopeChangeRequestOperationResultDto>(
            HttpMethod.Post,
            $"/api/service-appointments/{appointmentId}/scope-changes/{scopeChangeRequestId}/reject",
            request);
        return response.Payload ?? new ServiceScopeChangeRequestOperationResultDto(false, null, "api_error", response.ErrorMessage);
    }

    public async Task<IReadOnlyList<ServiceScopeChangeRequestDto>> GetScopeChangeRequestsByServiceRequestAsync(
        Guid actorUserId,
        string actorRole,
        Guid serviceRequestId)
    {
        var response = await _apiCaller.SendAsync<List<ServiceScopeChangeRequestDto>>(HttpMethod.Get, $"/api/service-appointments/service-requests/{serviceRequestId}/scope-changes");
        return response.Payload ?? [];
    }

    public async Task<IReadOnlyList<ServiceWarrantyClaimDto>> GetWarrantyClaimsByServiceRequestAsync(
        Guid actorUserId,
        string actorRole,
        Guid serviceRequestId)
    {
        var response = await _apiCaller.SendAsync<List<ServiceWarrantyClaimDto>>(HttpMethod.Get, $"/api/service-appointments/service-requests/{serviceRequestId}/warranty-claims");
        return response.Payload ?? [];
    }

    public async Task<IReadOnlyList<ServiceDisputeCaseDto>> GetDisputeCasesByServiceRequestAsync(
        Guid actorUserId,
        string actorRole,
        Guid serviceRequestId)
    {
        var response = await _apiCaller.SendAsync<List<ServiceDisputeCaseDto>>(HttpMethod.Get, $"/api/service-appointments/service-requests/{serviceRequestId}/disputes");
        return response.Payload ?? [];
    }

    public Task<ServiceCompletionPinResultDto> GenerateCompletionPinAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        GenerateServiceCompletionPinRequestDto request) =>
        Task.FromResult(new ServiceCompletionPinResultDto(false, null, null, "not_supported", "Operacao nao suportada no portal cliente."));

    public Task<ServiceCompletionPinResultDto> ValidateCompletionPinAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        ValidateServiceCompletionPinRequestDto request) =>
        Task.FromResult(new ServiceCompletionPinResultDto(false, null, null, "not_supported", "Operacao nao suportada no portal cliente."));

    public async Task<ServiceCompletionPinResultDto> ConfirmCompletionAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        ConfirmServiceCompletionRequestDto request)
    {
        var response = await _apiCaller.SendAsync<ServiceCompletionPinResultDto>(
            HttpMethod.Post,
            $"/api/service-appointments/{appointmentId}/completion/confirm",
            request);
        return response.Payload ?? new ServiceCompletionPinResultDto(false, null, null, "api_error", response.ErrorMessage);
    }

    public async Task<ServiceCompletionPinResultDto> ContestCompletionAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        ContestServiceCompletionRequestDto request)
    {
        var response = await _apiCaller.SendAsync<ServiceCompletionPinResultDto>(
            HttpMethod.Post,
            $"/api/service-appointments/{appointmentId}/completion/contest",
            request);
        return response.Payload ?? new ServiceCompletionPinResultDto(false, null, null, "api_error", response.ErrorMessage);
    }

    public async Task<ServiceCompletionPinResultDto> GetCompletionTermAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId)
    {
        var response = await _apiCaller.SendAsync<ServiceCompletionPinResultDto>(HttpMethod.Get, $"/api/service-appointments/{appointmentId}/completion");
        return response.Payload ?? new ServiceCompletionPinResultDto(false, null, null, "api_error", response.ErrorMessage);
    }

    public Task<int> ExpirePendingAppointmentsAsync(int batchSize = 200) =>
        Task.FromResult(0);

    public Task<int> ExpirePendingScopeChangeRequestsAsync(int batchSize = 200) =>
        Task.FromResult(0);

    public Task<int> EscalateWarrantyClaimsBySlaAsync(int batchSize = 200) =>
        Task.FromResult(0);

    public async Task<ServiceAppointmentOperationResultDto> GetByIdAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId)
    {
        var response = await _apiCaller.SendAsync<ServiceAppointmentOperationResultDto>(HttpMethod.Get, $"/api/service-appointments/{appointmentId}");
        return response.Payload ?? new ServiceAppointmentOperationResultDto(false, null, "api_error", response.ErrorMessage);
    }

    public async Task<IReadOnlyList<ServiceAppointmentDto>> GetMyAppointmentsAsync(
        Guid actorUserId,
        string actorRole,
        DateTime? fromUtc = null,
        DateTime? toUtc = null)
    {
        var query = new List<string>();
        if (fromUtc.HasValue)
        {
            query.Add($"fromUtc={Uri.EscapeDataString(fromUtc.Value.ToString("O", CultureInfo.InvariantCulture))}");
        }

        if (toUtc.HasValue)
        {
            query.Add($"toUtc={Uri.EscapeDataString(toUtc.Value.ToString("O", CultureInfo.InvariantCulture))}");
        }

        var path = "/api/service-appointments/mine";
        if (query.Count > 0)
        {
            path += "?" + string.Join("&", query);
        }

        var response = await _apiCaller.SendAsync<List<ServiceAppointmentDto>>(HttpMethod.Get, path);
        return response.Payload ?? [];
    }
}
