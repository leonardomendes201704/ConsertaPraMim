using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminChecklistTemplateService
{
    Task<IReadOnlyList<AdminChecklistTemplateDto>> GetAllAsync(bool includeInactive = true);
    Task<AdminChecklistTemplateUpsertResultDto> CreateAsync(
        AdminCreateChecklistTemplateRequestDto request,
        Guid actorUserId,
        string actorEmail);
    Task<AdminChecklistTemplateUpsertResultDto> UpdateAsync(
        Guid templateId,
        AdminUpdateChecklistTemplateRequestDto request,
        Guid actorUserId,
        string actorEmail);
    Task<AdminOperationResultDto> UpdateStatusAsync(
        Guid templateId,
        AdminUpdateChecklistTemplateStatusRequestDto request,
        Guid actorUserId,
        string actorEmail);
}
