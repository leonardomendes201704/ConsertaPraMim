using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminChangeLogsController : Controller
{
    private readonly IAdminChangeLogsService _adminChangeLogsService;

    public AdminChangeLogsController(IAdminChangeLogsService adminChangeLogsService)
    {
        _adminChangeLogsService = adminChangeLogsService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery(Name = "q")] string? searchTerm,
        [FromQuery(Name = "de")] DateTime? fromDate,
        [FromQuery(Name = "ate")] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var viewModel = await _adminChangeLogsService.BuildViewModelAsync(
            searchTerm,
            fromDate.HasValue ? DateOnly.FromDateTime(fromDate.Value.Date) : null,
            toDate.HasValue ? DateOnly.FromDateTime(toDate.Value.Date) : null,
            cancellationToken);

        return View(viewModel);
    }
}
