using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IMobileClientOrderService
{
    Task<MobileClientOrdersResponseDto> GetMyOrdersAsync(Guid clientUserId, int takePerBucket = 100);
    Task<MobileClientOrderDetailsResponseDto?> GetOrderDetailsAsync(Guid clientUserId, Guid orderId);
    Task<MobileClientOrderProposalDetailsResponseDto?> GetOrderProposalDetailsAsync(Guid clientUserId, Guid orderId, Guid proposalId);
    Task<MobileClientAcceptProposalResponseDto?> AcceptProposalAsync(Guid clientUserId, Guid orderId, Guid proposalId);
}
