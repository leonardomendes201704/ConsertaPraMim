using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IMobileProviderService
{
    Task<MobileProviderDashboardResponseDto> GetDashboardAsync(Guid providerUserId, int takeNearbyRequests = 20, int takeAgenda = 10);

    Task<MobileProviderRequestsResponseDto> GetNearbyRequestsAsync(Guid providerUserId, string? searchTerm = null, int take = 50);

    Task<MobileProviderRequestDetailsResponseDto?> GetRequestDetailsAsync(Guid providerUserId, Guid requestId);

    Task<MobileProviderProposalsResponseDto> GetMyProposalsAsync(Guid providerUserId, int take = 100);

    Task<MobileProviderProposalOperationResultDto> CreateProposalAsync(
        Guid providerUserId,
        Guid requestId,
        MobileProviderCreateProposalRequestDto request);

    Task<MobileProviderAgendaResponseDto> GetAgendaAsync(
        Guid providerUserId,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? statusFilter = null,
        int take = 50);

    Task<MobileProviderAgendaOperationResultDto> ConfirmAgendaAppointmentAsync(
        Guid providerUserId,
        Guid appointmentId);

    Task<MobileProviderAgendaOperationResultDto> RejectAgendaAppointmentAsync(
        Guid providerUserId,
        Guid appointmentId,
        MobileProviderRejectAgendaRequestDto request);

    Task<MobileProviderAgendaOperationResultDto> RespondAgendaRescheduleAsync(
        Guid providerUserId,
        Guid appointmentId,
        MobileProviderRespondRescheduleRequestDto request);
}
