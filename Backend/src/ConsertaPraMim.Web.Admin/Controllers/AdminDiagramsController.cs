using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminDiagramsController : Controller
{
    private readonly IAdminDiagramsService _adminDiagramsService;

    public AdminDiagramsController(IAdminDiagramsService adminDiagramsService)
    {
        _adminDiagramsService = adminDiagramsService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery(Name = "diag")] string? selectedDiagramPath, CancellationToken cancellationToken)
    {
        var viewModel = await _adminDiagramsService.BuildViewModelAsync(selectedDiagramPath, cancellationToken);
        return View(viewModel);
    }
}
