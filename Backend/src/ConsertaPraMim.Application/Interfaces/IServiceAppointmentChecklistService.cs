using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IServiceAppointmentChecklistService
{
    Task<ServiceAppointmentChecklistResultDto> GetChecklistAsync(Guid actorUserId, string actorRole, Guid appointmentId);

    Task<ServiceAppointmentChecklistResultDto> UpsertItemResponseAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        UpsertServiceChecklistItemResponseRequestDto request);

    Task<ServiceAppointmentChecklistValidationResultDto> ValidateRequiredItemsForCompletionAsync(
        Guid appointmentId,
        string? actorRole = null);
}
