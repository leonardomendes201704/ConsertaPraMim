namespace ConsertaPraMim.Application.DTOs;

public record MobileClientOrderItemDto(
    Guid Id,
    string Title,
    string Status,
    string Category,
    string Date,
    string Icon,
    string? Description);

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
    DateTime OccurredAtUtc);

public record MobileClientOrderDetailsResponseDto(
    MobileClientOrderItemDto Order,
    IReadOnlyList<MobileClientOrderFlowStepDto> FlowSteps,
    IReadOnlyList<MobileClientOrderTimelineEventDto> Timeline);
