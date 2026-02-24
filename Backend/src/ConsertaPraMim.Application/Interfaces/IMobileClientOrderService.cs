using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IMobileClientOrderService
{
    Task<MobileClientOrdersResponseDto> GetMyOrdersAsync(Guid clientUserId, int takePerBucket = 100);
    Task<MobileClientOrderDetailsResponseDto?> GetOrderDetailsAsync(Guid clientUserId, Guid orderId);
    Task<MobileClientOrderProposalDetailsResponseDto?> GetOrderProposalDetailsAsync(Guid clientUserId, Guid orderId, Guid proposalId);
    Task<MobileClientProposalComparisonResponseDto?> GetOrderProposalComparisonAsync(Guid clientUserId, Guid orderId, string? sortBy = null);
    Task<bool> TrackProposalComparisonInteractionAsync(Guid clientUserId, Guid orderId, MobileClientProposalComparisonInteractionRequestDto request);
    Task<MobileClientProposalComparisonAbSummaryDto> GetProposalComparisonAbSummaryAsync(DateTime fromUtc, DateTime toUtc);
    Task<MobileClientAcceptProposalResponseDto?> AcceptProposalAsync(Guid clientUserId, Guid orderId, Guid proposalId);
}
