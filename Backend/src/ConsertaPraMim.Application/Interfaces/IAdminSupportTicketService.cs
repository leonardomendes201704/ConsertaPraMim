using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminSupportTicketService
{
    Task<AdminSupportTicketListResponseDto> GetTicketsAsync(AdminSupportTicketListQueryDto query);

    Task<AdminSupportTicketOperationResultDto> GetTicketDetailsAsync(Guid ticketId);

    Task<AdminSupportTicketOperationResultDto> AddMessageAsync(
        Guid ticketId,
        Guid actorUserId,
        string actorEmail,
        AdminSupportTicketMessageRequestDto request);

    Task<AdminSupportTicketOperationResultDto> UpdateStatusAsync(
        Guid ticketId,
        Guid actorUserId,
        string actorEmail,
        AdminSupportTicketStatusUpdateRequestDto request);

    Task<AdminSupportTicketOperationResultDto> AssignAsync(
        Guid ticketId,
        Guid actorUserId,
        string actorEmail,
        AdminSupportTicketAssignRequestDto request);
}
