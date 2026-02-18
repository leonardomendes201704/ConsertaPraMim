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

public record MobileProviderCoverageMapPinDto(
    Guid RequestId,
    string Category,
    string CategoryIcon,
    string Description,
    string Street,
    string City,
    string Zip,
    DateTime CreatedAtUtc,
    double Latitude,
    double Longitude,
    double DistanceKm,
    bool IsWithinInterestRadius,
    bool IsCategoryMatch);

public record MobileProviderCoverageMapDto(
    bool HasBaseLocation,
    double? ProviderLatitude,
    double? ProviderLongitude,
    double? InterestRadiusKm,
    double? MapSearchRadiusKm,
    string? BaseZipCode,
    string? AppliedCategoryFilter,
    double? AppliedMaxDistanceKm,
    int PinPage,
    int PinPageSize,
    int TotalPins,
    bool HasMorePins,
    IReadOnlyList<MobileProviderCoverageMapPinDto> Pins);

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

public record MobileProviderMarkArrivalRequestDto(
    double? Latitude,
    double? Longitude,
    double? AccuracyMeters,
    string? ManualReason = null);

public record MobileProviderStartExecutionRequestDto(string? Reason = null);

public record MobileProviderUpdateOperationalStatusRequestDto(
    string OperationalStatus,
    string? Reason = null);

public record MobileProviderChecklistItemUpsertRequestDto(
    Guid TemplateItemId,
    bool IsChecked,
    string? Note,
    string? EvidenceUrl,
    string? EvidenceFileName,
    string? EvidenceContentType,
    long? EvidenceSizeBytes,
    bool ClearEvidence = false);

public record MobileProviderChecklistResultDto(
    bool Success,
    ServiceAppointmentChecklistDto? Checklist = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record MobileProviderChatAttachmentInputDto(
    string FileUrl,
    string FileName,
    string ContentType,
    long SizeBytes);

public record MobileProviderChatAttachmentDto(
    Guid Id,
    string FileUrl,
    string FileName,
    string ContentType,
    long SizeBytes,
    string MediaKind);

public record MobileProviderChatMessageDto(
    Guid Id,
    Guid RequestId,
    Guid ProviderId,
    Guid SenderId,
    string SenderName,
    string SenderRole,
    string? Text,
    DateTime CreatedAt,
    IReadOnlyList<MobileProviderChatAttachmentDto> Attachments,
    DateTime? DeliveredAt,
    DateTime? ReadAt);

public record MobileProviderChatMessageReceiptDto(
    Guid MessageId,
    Guid RequestId,
    Guid ProviderId,
    DateTime? DeliveredAt,
    DateTime? ReadAt);

public record MobileProviderChatConversationSummaryDto(
    Guid RequestId,
    Guid ProviderId,
    Guid CounterpartUserId,
    string CounterpartRole,
    string CounterpartName,
    string Title,
    string LastMessagePreview,
    DateTime LastMessageAt,
    int UnreadMessages,
    bool CounterpartIsOnline,
    string? ProviderStatus);

public record MobileProviderChatConversationsResponseDto(
    IReadOnlyList<MobileProviderChatConversationSummaryDto> Conversations,
    int TotalCount,
    int TotalUnreadMessages);

public record MobileProviderChatMessagesResponseDto(
    Guid RequestId,
    Guid ProviderId,
    IReadOnlyList<MobileProviderChatMessageDto> Messages,
    int TotalCount);

public record MobileProviderSendChatMessageRequestDto(
    string? Text,
    IReadOnlyList<MobileProviderChatAttachmentInputDto>? Attachments);

public record MobileProviderSendChatMessageResponseDto(
    bool Success,
    MobileProviderChatMessageDto? Message = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record MobileProviderChatReceiptOperationResponseDto(
    bool Success,
    IReadOnlyList<MobileProviderChatMessageReceiptDto> Receipts,
    string? ErrorCode = null,
    string? ErrorMessage = null);
