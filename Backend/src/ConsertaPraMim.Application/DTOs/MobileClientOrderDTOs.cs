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
