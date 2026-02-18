using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;

namespace ConsertaPraMim.Web.Client.Services;

public class ClientApiServiceCategoryCatalogService : IServiceCategoryCatalogService
{
    private readonly ClientApiCaller _apiCaller;

    public ClientApiServiceCategoryCatalogService(ClientApiCaller apiCaller)
    {
        _apiCaller = apiCaller;
    }

    public async Task<IReadOnlyList<ServiceCategoryOptionDto>> GetActiveAsync()
    {
        var response = await _apiCaller.SendAsync<List<ServiceCategoryOptionDto>>(HttpMethod.Get, "/api/service-categories/active");
        return response.Payload ?? [];
    }
}
