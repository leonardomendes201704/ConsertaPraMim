using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminGrowthAiService
{
    Task<AdminGrowthAiSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default);

    Task<AdminOperationResultDto> UpsertSettingsAsync(
        AdminGrowthAiUpsertSettingsRequestDto request,
        Guid actorUserId,
        string? actorEmail,
        CancellationToken cancellationToken = default);

    Task<AdminGrowthAiAnalyzeResultDto> AnalyzeAsync(
        AdminGrowthAiAnalyzeRequestDto request,
        Guid actorUserId,
        string? actorEmail,
        CancellationToken cancellationToken = default);

    Task<AdminGrowthAiCompareResultDto> CompareAsync(
        AdminGrowthAiCompareRequestDto request,
        Guid actorUserId,
        string? actorEmail,
        CancellationToken cancellationToken = default);
}
