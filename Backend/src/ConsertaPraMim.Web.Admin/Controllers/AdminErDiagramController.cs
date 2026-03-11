using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminErDiagramController : Controller
{
    private readonly IAdminDatabaseSchemaService _databaseSchemaService;

    public AdminErDiagramController(IAdminDatabaseSchemaService databaseSchemaService)
    {
        _databaseSchemaService = databaseSchemaService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var viewModel = await _databaseSchemaService.BuildViewModelAsync(cancellationToken);
        return View(viewModel);
    }
}
