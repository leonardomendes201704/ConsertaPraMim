using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Repositories;
using ConsertaPraMim.Domain.Enums;
using System.Linq;
using ConsertaPraMim.Web.Provider.Options;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.Provider.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminController : Controller
{
    private readonly IUserRepository _userRepository;
    private readonly IServiceRequestRepository _requestRepository;
    private readonly LegacyAdminOptions _legacyAdminOptions;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IUserRepository userRepository,
        IServiceRequestRepository requestRepository,
        IOptions<LegacyAdminOptions> legacyAdminOptions,
        ILogger<AdminController> logger)
    {
        _userRepository = userRepository;
        _requestRepository = requestRepository;
        _legacyAdminOptions = legacyAdminOptions.Value;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var disabledResult = EnsureLegacyAdminEnabled();
        if (disabledResult != null) return disabledResult;

        var users = await _userRepository.GetAllAsync();
        var requests = await _requestRepository.GetAllAsync();

        ViewBag.TotalUsers = users.Count();
        ViewBag.TotalProviders = users.Count(u => u.Role == UserRole.Provider);
        ViewBag.TotalClients = users.Count(u => u.Role == UserRole.Client);
        ViewBag.TotalRequests = requests.Count();
        ViewBag.ActiveRequests = requests.Count(r => r.Status != ServiceRequestStatus.Completed && r.Status != ServiceRequestStatus.Canceled);

        return View(users.OrderByDescending(u => u.CreatedAt).Take(10));
    }

    public async Task<IActionResult> Users()
    {
        var disabledResult = EnsureLegacyAdminEnabled();
        if (disabledResult != null) return disabledResult;

        var users = await _userRepository.GetAllAsync();
        return View(users);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleUserStatus(Guid id)
    {
        var disabledResult = EnsureLegacyAdminEnabled();
        if (disabledResult != null) return disabledResult;

        var user = await _userRepository.GetByIdAsync(id);
        if (user != null)
        {
            user.IsActive = !user.IsActive;
            await _userRepository.UpdateAsync(user);
        }
        return RedirectToAction("Users");
    }

    private IActionResult? EnsureLegacyAdminEnabled()
    {
        if (_legacyAdminOptions.Enabled)
        {
            return null;
        }

        _logger.LogInformation(
            "Legacy provider admin route blocked because feature flag is disabled. Path={Path}, User={User}",
            HttpContext?.Request?.Path.Value ?? "(unknown)",
            User?.Identity?.Name ?? "anonymous");

        return NotFound();
    }
}
