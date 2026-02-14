using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IServiceAppointmentService
{
    Task<ServiceAppointmentSlotsResultDto> GetAvailableSlotsAsync(
        Guid actorUserId,
        string actorRole,
        GetServiceAppointmentSlotsQueryDto query);

    Task<ServiceAppointmentOperationResultDto> CreateAsync(
        Guid actorUserId,
        string actorRole,
        CreateServiceAppointmentRequestDto request);

    Task<ServiceAppointmentOperationResultDto> ConfirmAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId);

    Task<ServiceAppointmentOperationResultDto> RejectAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId,
        RejectServiceAppointmentRequestDto request);

    Task<int> ExpirePendingAppointmentsAsync(int batchSize = 200);

    Task<ServiceAppointmentOperationResultDto> GetByIdAsync(
        Guid actorUserId,
        string actorRole,
        Guid appointmentId);

    Task<IReadOnlyList<ServiceAppointmentDto>> GetMyAppointmentsAsync(
        Guid actorUserId,
        string actorRole,
        DateTime? fromUtc = null,
        DateTime? toUtc = null);
}
