using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;

namespace ConsertaPraMim.Web.Provider.Services;

public class ProviderApiServiceAppointmentChecklistService : IServiceAppointmentChecklistService
{
    private readonly ProviderApiCaller _apiCaller;

    public ProviderApiServiceAppointmentChecklistService(ProviderApiCaller apiCaller)
    {
        _apiCaller = apiCaller;
    }

    public async Task<ServiceAppointmentChecklistResultDto> GetChecklistAsync(Guid actorUserId, string actorRole, Guid appointmentId)
    {
        var response = await _apiCaller.SendAsync<ServiceAppointmentChecklistResultDto>(HttpMethod.Get, $"/api/service-appointments/{appointmentId}/checklist");
        return response.Payload ?? new ServiceAppointmentChecklistResultDto(false, null, "api_error", response.ErrorMessage);
    }

    public async Task<ServiceAppointmentChecklistResultDto> UpsertItemResponseAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        UpsertServiceChecklistItemResponseRequestDto request)
    {
        var response = await _apiCaller.SendAsync<ServiceAppointmentChecklistResultDto>(
            HttpMethod.Post,
            $"/api/service-appointments/{appointmentId}/checklist/items/{request.TemplateItemId}",
            request);
        return response.Payload ?? new ServiceAppointmentChecklistResultDto(false, null, "api_error", response.ErrorMessage);
    }

    public Task<ServiceAppointmentChecklistValidationResultDto> ValidateRequiredItemsForCompletionAsync(
        Guid appointmentId,
        string? actorRole = null) =>
        Task.FromResult(new ServiceAppointmentChecklistValidationResultDto(false, false, 0, [], "not_supported", "Operacao nao suportada no portal prestador."));
}

