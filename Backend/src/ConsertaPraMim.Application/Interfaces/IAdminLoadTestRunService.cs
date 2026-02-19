using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminLoadTestRunService
{
    Task<AdminLoadTestRunsResponseDto> GetRunsAsync(
        AdminLoadTestRunsQueryDto query,
        CancellationToken cancellationToken = default);

    Task<AdminLoadTestRunDetailsDto?> GetRunByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<AdminLoadTestImportResultDto> ImportRunAsync(
        AdminLoadTestImportRequestDto request,
        CancellationToken cancellationToken = default);
}
