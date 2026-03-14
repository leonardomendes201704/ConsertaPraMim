using AppMobileCPM.Areas.Admin.ViewModels;
using AppMobileCPM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppMobileCPM.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(AuthenticationSchemes = AdminAuthConstants.AuthenticationScheme)]
[Route("admin")]
public sealed class DashboardController : Controller
{
    private readonly IAdminAuthService _adminAuthService;

    public DashboardController(IAdminAuthService adminAuthService)
    {
        _adminAuthService = adminAuthService;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        var stats = _adminAuthService.GetDashboardStats();
        return View(new AdminDashboardViewModel
        {
            AdminDisplayName = User.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value ?? "Administrador",
            ProfessionalsCount = stats.ProfessionalsCount,
            ServiceRequestsCount = stats.ServiceRequestsCount,
            ProfessionalRegistrationsCount = stats.ProfessionalRegistrationsCount,
            SupportRequestsCount = stats.SupportRequestsCount
        });
    }
}
