using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/service-categories")]
public class ServiceCategoriesController : ControllerBase
{
    private readonly IServiceCategoryCatalogService _serviceCategoryCatalogService;

    public ServiceCategoriesController(IServiceCategoryCatalogService serviceCategoryCatalogService)
    {
        _serviceCategoryCatalogService = serviceCategoryCatalogService;
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var categories = await _serviceCategoryCatalogService.GetActiveAsync();
        return Ok(categories);
    }
}
