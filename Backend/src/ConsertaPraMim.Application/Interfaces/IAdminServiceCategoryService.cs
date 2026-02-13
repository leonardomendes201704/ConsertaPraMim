using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminServiceCategoryService
{
    Task<IReadOnlyList<AdminServiceCategoryDto>> GetAllAsync(bool includeInactive = true);
    Task<AdminServiceCategoryUpsertResultDto> CreateAsync(
        AdminCreateServiceCategoryRequestDto request,
        Guid actorUserId,
        string actorEmail);
    Task<AdminServiceCategoryUpsertResultDto> UpdateAsync(
        Guid categoryId,
        AdminUpdateServiceCategoryRequestDto request,
        Guid actorUserId,
        string actorEmail);
    Task<AdminOperationResultDto> UpdateStatusAsync(
        Guid categoryId,
        AdminUpdateServiceCategoryStatusRequestDto request,
        Guid actorUserId,
        string actorEmail);
}
