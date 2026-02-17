using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class ServiceCategoryCatalogService : IServiceCategoryCatalogService
{
    private readonly IServiceCategoryRepository _serviceCategoryRepository;

    public ServiceCategoryCatalogService(IServiceCategoryRepository serviceCategoryRepository)
    {
        _serviceCategoryRepository = serviceCategoryRepository;
    }

    public async Task<IReadOnlyList<ServiceCategoryOptionDto>> GetActiveAsync()
    {
        var categories = await _serviceCategoryRepository.GetActiveAsync();
        return categories
            .OrderBy(c => c.Name)
            .Select(c => new ServiceCategoryOptionDto(
                c.Id,
                c.Name,
                c.Slug,
                c.LegacyCategory.ToString(),
                string.IsNullOrWhiteSpace(c.Icon) ? "build_circle" : c.Icon))
            .ToList();
    }
}
