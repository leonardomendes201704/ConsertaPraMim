using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminWikiController : Controller
{
    private readonly IAdminWikiService _adminWikiService;

    public AdminWikiController(IAdminWikiService adminWikiService)
    {
        _adminWikiService = adminWikiService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery(Name = "doc")] string? selectedDocumentPath, CancellationToken cancellationToken)
    {
        var viewModel = await _adminWikiService.BuildViewModelAsync(selectedDocumentPath, cancellationToken);
        return View(viewModel);
    }
}
