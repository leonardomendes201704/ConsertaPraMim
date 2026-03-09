using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IAdminLandingAnalyticsService
{
    Task<AdminLandingAnalyticsOverviewDto> GetOverviewAsync(
        AdminLandingAnalyticsQueryDto query,
        CancellationToken cancellationToken = default);

    Task<AdminLandingAnalyticsInsightsDto> GetInsightsAsync(
        AdminLandingAnalyticsQueryDto query,
        CancellationToken cancellationToken = default);

    Task<AdminLandingAnalyticsSessionDetailsDto?> GetSessionDetailsAsync(
        string sessionId,
        CancellationToken cancellationToken = default);
}
