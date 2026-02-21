using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IMobileProviderService
{
    Task<MobileProviderDashboardResponseDto> GetDashboardAsync(Guid providerUserId, int takeNearbyRequests = 20, int takeAgenda = 10);

    Task<MobileProviderProfileSettingsDto?> GetProfileSettingsAsync(Guid providerUserId);

    Task<MobileProviderProfileSettingsOperationResultDto> UpdateProfileSettingsAsync(
        Guid providerUserId,
        MobileProviderUpdateProfileSettingsRequestDto request);

    Task<MobileProviderProfileSettingsOperationResultDto> UpdateProfileOperationalStatusAsync(
        Guid providerUserId,
        MobileProviderUpdateProfileOperationalStatusRequestDto request);

    Task<MobileProviderCoverageMapDto> GetCoverageMapAsync(
        Guid providerUserId,
        string? categoryFilter = null,
        double? maxDistanceKm = null,
        int pinPage = 1,
        int pinPageSize = 120);

    Task<MobileProviderRequestsResponseDto> GetNearbyRequestsAsync(Guid providerUserId, string? searchTerm = null, int take = 50);

    Task<MobileProviderRequestDetailsResponseDto?> GetRequestDetailsAsync(Guid providerUserId, Guid requestId);

    Task<MobileProviderProposalsResponseDto> GetMyProposalsAsync(Guid providerUserId, int take = 100);

    Task<MobileProviderSupportTicketOperationResultDto> CreateSupportTicketAsync(
        Guid providerUserId,
        MobileProviderCreateSupportTicketRequestDto request);

    Task<MobileProviderSupportTicketListResponseDto> GetSupportTicketsAsync(
        Guid providerUserId,
        MobileProviderSupportTicketListQueryDto query);

    Task<MobileProviderSupportTicketOperationResultDto> GetSupportTicketDetailsAsync(
        Guid providerUserId,
        Guid ticketId);

    Task<MobileProviderSupportTicketOperationResultDto> AddSupportTicketMessageAsync(
        Guid providerUserId,
        Guid ticketId,
        MobileProviderSupportTicketMessageRequestDto request);

    Task<MobileProviderSupportTicketOperationResultDto> CloseSupportTicketAsync(
        Guid providerUserId,
        Guid ticketId);

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

    Task<MobileProviderAgendaOperationResultDto> MarkAgendaArrivalAsync(
        Guid providerUserId,
        Guid appointmentId,
        MobileProviderMarkArrivalRequestDto request);

    Task<MobileProviderAgendaOperationResultDto> StartAgendaExecutionAsync(
        Guid providerUserId,
        Guid appointmentId,
        MobileProviderStartExecutionRequestDto request);

    Task<MobileProviderAgendaOperationResultDto> UpdateAgendaOperationalStatusAsync(
        Guid providerUserId,
        Guid appointmentId,
        MobileProviderUpdateOperationalStatusRequestDto request);

    Task<MobileProviderChecklistResultDto> GetAppointmentChecklistAsync(
        Guid providerUserId,
        Guid appointmentId);

    Task<MobileProviderChecklistResultDto> UpdateAppointmentChecklistItemAsync(
        Guid providerUserId,
        Guid appointmentId,
        MobileProviderChecklistItemUpsertRequestDto request);

    Task<MobileProviderChatConversationsResponseDto> GetChatConversationsAsync(Guid providerUserId);

    Task<MobileProviderChatMessagesResponseDto> GetChatMessagesAsync(Guid providerUserId, Guid requestId);

    Task<MobileProviderSendChatMessageResponseDto> SendChatMessageAsync(
        Guid providerUserId,
        Guid requestId,
        MobileProviderSendChatMessageRequestDto request);

    Task<MobileProviderChatReceiptOperationResponseDto> MarkChatConversationDeliveredAsync(
        Guid providerUserId,
        Guid requestId);

    Task<MobileProviderChatReceiptOperationResponseDto> MarkChatConversationReadAsync(
        Guid providerUserId,
        Guid requestId);
}
