using ConsertaPraMim.Application.DTOs;

namespace ConsertaPraMim.Application.Interfaces;

public interface IServiceCategoryCatalogService
{
    Task<IReadOnlyList<ServiceCategoryOptionDto>> GetActiveAsync();
}
