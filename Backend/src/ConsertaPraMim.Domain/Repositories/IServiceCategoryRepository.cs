using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Repositories;

public interface IServiceCategoryRepository
{
    Task<IReadOnlyList<ServiceCategoryDefinition>> GetAllAsync(bool includeInactive = true);
    Task<IReadOnlyList<ServiceCategoryDefinition>> GetActiveAsync();
    Task<ServiceCategoryDefinition?> GetByIdAsync(Guid id);
    Task<ServiceCategoryDefinition?> GetBySlugAsync(string slug);
    Task<ServiceCategoryDefinition?> GetByNameAsync(string name);
    Task<ServiceCategoryDefinition?> GetFirstActiveByLegacyAsync(ServiceCategory legacyCategory);
    Task AddAsync(ServiceCategoryDefinition category);
    Task UpdateAsync(ServiceCategoryDefinition category);
}
