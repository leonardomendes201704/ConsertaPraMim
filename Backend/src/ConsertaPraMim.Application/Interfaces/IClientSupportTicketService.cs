using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IClientSupportTicketService
{
    Task<ClientSupportTicketDetailsDto?> GetByServiceRequestAsync(Guid clientUserId, Guid serviceRequestId);

    Task<ClientSupportTicketOperationResultDto> AddMessageAsync(
        Guid clientUserId,
        Guid serviceRequestId,
        ClientSupportTicketMessageRequestDto request);
}
