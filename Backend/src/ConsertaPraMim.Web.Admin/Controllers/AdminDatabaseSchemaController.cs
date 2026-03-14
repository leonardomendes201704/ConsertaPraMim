using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminDatabaseSchemaController : Controller
{
    private readonly IAdminDatabaseSchemaService _databaseSchemaService;

    public AdminDatabaseSchemaController(IAdminDatabaseSchemaService databaseSchemaService)
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