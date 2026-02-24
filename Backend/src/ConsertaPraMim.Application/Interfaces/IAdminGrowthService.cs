using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminGrowthService
{
    Task<AdminGrowthFunnelDto> GetFunnelAsync(AdminGrowthFunnelQueryDto query);

    Task<AdminGrowthExecutiveCockpitDto> GetExecutiveCockpitAsync(
        AdminGrowthExecutiveCockpitQueryDto query,
        CancellationToken cancellationToken = default);

    Task<AdminProviderReactivationSegmentsDto> GetProviderReactivationSegmentsAsync(
        AdminProviderReactivationSegmentsQueryDto query,
        CancellationToken cancellationToken = default);

    Task<AdminProviderReactivationCampaignRunResultDto> RunProviderReactivationCampaignAsync(
        AdminProviderReactivationCampaignRunRequestDto request,
        Guid actorUserId,
        string actorEmail,
        CancellationToken cancellationToken = default);

    Task<AdminProviderReactivationCampaignPerformanceDto> GetProviderReactivationCampaignPerformanceAsync(
        AdminProviderReactivationCampaignPerformanceQueryDto query,
        CancellationToken cancellationToken = default);

    Task<AdminProviderReactivationPreferenceDto> UpsertProviderReactivationPreferenceAsync(
        AdminProviderReactivationPreferenceUpsertRequestDto request,
        Guid actorUserId,
        string actorEmail,
        CancellationToken cancellationToken = default);
}
