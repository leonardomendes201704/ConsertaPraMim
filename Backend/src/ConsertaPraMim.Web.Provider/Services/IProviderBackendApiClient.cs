using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Web.Provider.Services;

public interface IProviderBackendApiClient
{
    Task<(IReadOnlyList<ServiceRequestDto> Requests, string? ErrorMessage)> GetRequestsAsync(string? searchTerm = null, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<ServiceRequestDto> Requests, string? ErrorMessage)> GetHistoryAsync(CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<ProposalDto> Proposals, string? ErrorMessage)> GetMyProposalsAsync(CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<ServiceAppointmentDto> Appointments, string? ErrorMessage)> GetMyAppointmentsAsync(CancellationToken cancellationToken = default);
    Task<(UserProfileDto? Profile, string? ErrorMessage)> GetProfileAsync(CancellationToken cancellationToken = default);
    Task<(MobileProviderCoverageMapDto? CoverageMap, string? ErrorMessage)> GetCoverageMapAsync(
        string? categoryFilter = null,
        double? maxDistanceKm = null,
        int pinPage = 1,
        int pinPageSize = 120,
        CancellationToken cancellationToken = default);
    Task<(MobileProviderSupportTicketListResponseDto? Response, string? ErrorMessage)> GetSupportTicketsAsync(
        string? status = null,
        string? priority = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
    Task<(MobileProviderSupportTicketDetailsDto? Ticket, string? ErrorMessage)> CreateSupportTicketAsync(
        MobileProviderCreateSupportTicketRequestDto request,
        CancellationToken cancellationToken = default);
    Task<(MobileProviderSupportTicketDetailsDto? Ticket, string? ErrorMessage)> GetSupportTicketDetailsAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default);
    Task<(MobileProviderSupportTicketDetailsDto? Ticket, string? ErrorMessage)> AddSupportTicketMessageAsync(
        Guid ticketId,
        MobileProviderSupportTicketMessageRequestDto request,
        CancellationToken cancellationToken = default);
    Task<(MobileProviderSupportTicketDetailsDto? Ticket, string? ErrorMessage)> CloseSupportTicketAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage)> SubmitProposalAsync(CreateProposalDto dto, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<ChatConversationSummaryDto> Conversations, string? ErrorMessage)> GetConversationsAsync(CancellationToken cancellationToken = default);
}
