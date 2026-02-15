using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminNoShowAlertThresholdService
{
    Task<AdminNoShowAlertThresholdDto?> GetActiveAsync();

    Task<AdminNoShowAlertThresholdUpdateResultDto> UpdateActiveAsync(
        AdminUpdateNoShowAlertThresholdRequestDto request,
        Guid actorUserId,
        string actorEmail);
}
