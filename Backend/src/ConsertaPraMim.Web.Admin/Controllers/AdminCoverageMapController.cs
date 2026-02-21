using ConsertaPraMim.Web.Admin.Security;
using ConsertaPraMim.Web.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.Admin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminCoverageMapController : Controller
{
    private readonly IAdminDashboardApiClient _adminDashboardApiClient;

    public AdminCoverageMapController(IAdminDashboardApiClient adminDashboardApiClient)
    {
        _adminDashboardApiClient = adminDashboardApiClient;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Snapshot()
    {
        var token = User.FindFirst(AdminClaimTypes.ApiToken)?.Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new
            {
                success = false,
                errorMessage = "Token administrativo ausente. Faca login novamente."
            });
        }

        var result = await _adminDashboardApiClient.GetCoverageMapAsync(token, HttpContext.RequestAborted);
        if (!result.Success || result.CoverageMap == null)
        {
            var statusCode = result.StatusCode ?? StatusCodes.Status502BadGateway;
            return StatusCode(statusCode, new
            {
                success = false,
                errorMessage = result.ErrorMessage ?? "Falha ao carregar o mapa operacional."
            });
        }

        return Ok(new
        {
            success = true,
            data = result.CoverageMap,
            refreshedAtUtc = DateTime.UtcNow
        });
    }
}
