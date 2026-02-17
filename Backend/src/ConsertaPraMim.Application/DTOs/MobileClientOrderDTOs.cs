namespace ConsertaPraMim.Application.DTOs;

public record MobileClientOrderItemDto(
    Guid Id,
    string Title,
    string Status,
    string Category,
    string Date,
    string Icon,
    string? Description,
    int ProposalCount);

public record MobileClientOrdersResponseDto(
    IReadOnlyList<MobileClientOrderItemDto> OpenOrders,
    IReadOnlyList<MobileClientOrderItemDto> FinalizedOrders,
    int OpenOrdersCount,
    int FinalizedOrdersCount,
    int TotalOrdersCount);

public record MobileClientOrderFlowStepDto(
    int Step,
    string Title,
    bool Completed,
    bool Current);

public record MobileClientOrderTimelineEventDto(
    string EventCode,
    string Title,
    string Description,
    DateTime OccurredAtUtc,
    string? RelatedEntityType = null,
    Guid? RelatedEntityId = null);

public record MobileClientOrderDetailsResponseDto(
    MobileClientOrderItemDto Order,
    IReadOnlyList<MobileClientOrderFlowStepDto> FlowSteps,
    IReadOnlyList<MobileClientOrderTimelineEventDto> Timeline);

public record MobileClientOrderProposalDetailsDto(
    Guid Id,
    Guid OrderId,
    Guid ProviderId,
    string ProviderName,
    decimal? EstimatedValue,
    string? Message,
    bool Accepted,
    bool Invalidated,
    string StatusLabel,
    DateTime SentAtUtc);

public record MobileClientOrderProposalDetailsResponseDto(
    MobileClientOrderItemDto Order,
    MobileClientOrderProposalDetailsDto Proposal,
    MobileClientOrderProposalAppointmentDto? CurrentAppointment = null);

public record MobileClientAcceptProposalResponseDto(
    MobileClientOrderItemDto Order,
    MobileClientOrderProposalDetailsDto Proposal,
    string Message);

public record MobileClientOrderProposalSlotDto(
    DateTime WindowStartUtc,
    DateTime WindowEndUtc);

public record MobileClientOrderProposalSlotsResponseDto(
    Guid OrderId,
    Guid ProposalId,
    Guid ProviderId,
    DateOnly Date,
    IReadOnlyList<MobileClientOrderProposalSlotDto> Slots);

public record MobileClientOrderProposalScheduleRequestDto(
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    string? Reason = null);

public record MobileClientOrderProposalAppointmentDto(
    Guid Id,
    Guid OrderId,
    Guid ProposalId,
    Guid ProviderId,
    string ProviderName,
    string Status,
    string StatusLabel,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record MobileClientScheduleOrderProposalResponseDto(
    MobileClientOrderItemDto Order,
    MobileClientOrderProposalDetailsDto Proposal,
    MobileClientOrderProposalAppointmentDto Appointment,
    string Message);
