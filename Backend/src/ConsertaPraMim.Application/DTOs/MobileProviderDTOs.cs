namespace ConsertaPraMim.Application.DTOs;

public record MobileProviderDashboardKpiDto(
    int NearbyRequestsCount,
    int ActiveProposalsCount,
    int AcceptedProposalsCount,
    int PendingAppointmentsCount,
    int UpcomingConfirmedVisitsCount);

public record MobileProviderRequestCardDto(
    Guid Id,
    string Category,
    string CategoryIcon,
    string Description,
    string Status,
    DateTime CreatedAtUtc,
    string Street,
    string City,
    string Zip,
    double? DistanceKm,
    decimal? EstimatedValue,
    bool AlreadyProposed);

public record MobileProviderAppointmentHighlightDto(
    Guid AppointmentId,
    Guid ServiceRequestId,
    string Status,
    string StatusLabel,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    string? Category,
    string? ClientName);

public record MobileProviderDashboardResponseDto(
    string ProviderName,
    MobileProviderDashboardKpiDto Kpis,
    IReadOnlyList<MobileProviderRequestCardDto> NearbyRequests,
    IReadOnlyList<MobileProviderAppointmentHighlightDto> AgendaHighlights);

public record MobileProviderRequestsResponseDto(
    IReadOnlyList<MobileProviderRequestCardDto> Items,
    int TotalCount);

public record MobileProviderProposalSummaryDto(
    Guid Id,
    Guid RequestId,
    decimal? EstimatedValue,
    string? Message,
    bool Accepted,
    bool Invalidated,
    string StatusLabel,
    DateTime CreatedAtUtc);

public record MobileProviderProposalsResponseDto(
    IReadOnlyList<MobileProviderProposalSummaryDto> Items,
    int TotalCount,
    int AcceptedCount,
    int OpenCount);

public record MobileProviderRequestDetailsResponseDto(
    MobileProviderRequestCardDto Request,
    MobileProviderProposalSummaryDto? ExistingProposal,
    bool CanSubmitProposal);

public record MobileProviderCreateProposalRequestDto(
    decimal? EstimatedValue,
    string? Message);

public record MobileProviderCreateProposalResponseDto(
    MobileProviderProposalSummaryDto Proposal,
    string Message);

public record MobileProviderProposalOperationResultDto(
    bool Success,
    MobileProviderCreateProposalResponseDto? Payload = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record MobileProviderAgendaItemDto(
    Guid AppointmentId,
    Guid ServiceRequestId,
    string AppointmentStatus,
    string AppointmentStatusLabel,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    string? Category,
    string? Description,
    string? ClientName,
    string? Street,
    string? City,
    string? Zip,
    bool CanConfirm,
    bool CanReject,
    bool CanRespondReschedule);

public record MobileProviderAgendaResponseDto(
    IReadOnlyList<MobileProviderAgendaItemDto> PendingItems,
    IReadOnlyList<MobileProviderAgendaItemDto> UpcomingItems,
    int PendingCount,
    int UpcomingCount);

public record MobileProviderRejectAgendaRequestDto(string Reason);

public record MobileProviderRespondRescheduleRequestDto(
    bool Accept,
    string? Reason = null);

public record MobileProviderAgendaOperationResultDto(
    bool Success,
    MobileProviderAgendaItemDto? Item = null,
    string? Message = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);
