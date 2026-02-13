using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminChatNotificationService
{
    Task<AdminChatsListResponseDto> GetChatsAsync(AdminChatsQueryDto query);
    Task<AdminChatDetailsDto?> GetChatAsync(Guid requestId, Guid providerId);
    Task<AdminChatAttachmentsListResponseDto> GetChatAttachmentsAsync(AdminChatAttachmentsQueryDto query);
    Task<AdminSendNotificationResultDto> SendNotificationAsync(
        AdminSendNotificationRequestDto request,
        Guid actorUserId,
        string actorEmail);
}
