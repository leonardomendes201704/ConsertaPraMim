using ConsertaPraMim.Domain.Entities;

namespace ConsertaPraMim.Domain.Repositories;

public interface INoShowAlertThresholdConfigurationRepository
{
    Task<NoShowAlertThresholdConfiguration?> GetActiveAsync();
    Task<IReadOnlyList<NoShowAlertThresholdConfiguration>> GetAllAsync();
    Task AddAsync(NoShowAlertThresholdConfiguration configuration);
    Task UpdateAsync(NoShowAlertThresholdConfiguration configuration);
}
