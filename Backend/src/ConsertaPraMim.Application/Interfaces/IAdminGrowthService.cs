using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminGrowthService
{
    Task<AdminGrowthFunnelDto> GetFunnelAsync(AdminGrowthFunnelQueryDto query);

    Task<AdminProviderReactivationSegmentsDto> GetProviderReactivationSegmentsAsync(
        AdminProviderReactivationSegmentsQueryDto query,
        CancellationToken cancellationToken = default);

    Task<AdminProviderReactivationCampaignRunResultDto> RunProviderReactivationCampaignAsync(
        AdminProviderReactivationCampaignRunRequestDto request,
        Guid actorUserId,
        string actorEmail,
        CancellationToken cancellationToken = default);
}
