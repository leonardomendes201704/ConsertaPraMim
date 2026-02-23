using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminRoadmapController : Controller
{
    private readonly IAdminRoadmapService _adminRoadmapService;

    public AdminRoadmapController(IAdminRoadmapService adminRoadmapService)
    {
        _adminRoadmapService = adminRoadmapService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery(Name = "q")] string? searchTerm,
        [FromQuery(Name = "epic")] string? epicFilter,
        [FromQuery(Name = "trilha")] string? trackFilter,
        [FromQuery(Name = "status")] string? statusFilter,
        CancellationToken cancellationToken)
    {
        var viewModel = await _adminRoadmapService.BuildViewModelAsync(
            searchTerm,
            epicFilter,
            trackFilter,
            statusFilter,
            cancellationToken);

        return View(viewModel);
    }
}
