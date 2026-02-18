using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Web.Provider.Services;
using System.Linq;
using ConsertaPraMim.Web.Provider.Options;
using Microsoft.Extensions.Options;

namespace ConsertaPraMim.Web.Provider.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AdminController : Controller
{
    private readonly IProviderLegacyAdminApiClient _legacyAdminApiClient;
    private readonly LegacyAdminOptions _legacyAdminOptions;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IProviderLegacyAdminApiClient legacyAdminApiClient,
        IOptions<LegacyAdminOptions> legacyAdminOptions,
        ILogger<AdminController> logger)
    {
        _legacyAdminApiClient = legacyAdminApiClient;
        _legacyAdminOptions = legacyAdminOptions.Value;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var disabledResult = EnsureLegacyAdminEnabled();
        if (disabledResult != null) return disabledResult;

        var (dashboard, dashboardError) = await _legacyAdminApiClient.GetDashboardAsync(HttpContext.RequestAborted);
        var (users, _, usersError) = await _legacyAdminApiClient.GetUsersAsync(
            page: 1,
            pageSize: 10,
            cancellationToken: HttpContext.RequestAborted);

        if (!string.IsNullOrWhiteSpace(dashboardError) || !string.IsNullOrWhiteSpace(usersError))
        {
            TempData["Error"] = dashboardError ?? usersError;
        }

        ViewBag.TotalUsers = dashboard?.TotalUsers ?? users.Count;
        ViewBag.TotalProviders = dashboard?.TotalProviders ?? users.Count(u => string.Equals(u.Role, UserRole.Provider.ToString(), StringComparison.OrdinalIgnoreCase));
        ViewBag.TotalClients = dashboard?.TotalClients ?? users.Count(u => string.Equals(u.Role, UserRole.Client.ToString(), StringComparison.OrdinalIgnoreCase));
        ViewBag.TotalRequests = dashboard?.TotalRequests ?? 0;
        ViewBag.ActiveRequests = dashboard?.ActiveRequests ?? 0;

        var model = users.Select(MapToDomainUser).OrderByDescending(u => u.CreatedAt).Take(10).ToList();
        return View(model);
    }

    public async Task<IActionResult> Users()
    {
        var disabledResult = EnsureLegacyAdminEnabled();
        if (disabledResult != null) return disabledResult;

        const int pageSize = 200;
        var page = 1;
        var totalCount = int.MaxValue;
        var allUsers = new List<User>();

        while (allUsers.Count < totalCount)
        {
            var (usersPage, total, error) = await _legacyAdminApiClient.GetUsersAsync(
                page: page,
                pageSize: pageSize,
                cancellationToken: HttpContext.RequestAborted);
            if (!string.IsNullOrWhiteSpace(error))
            {
                TempData["Error"] = error;
                break;
            }

            totalCount = total;
            if (usersPage.Count == 0)
            {
                break;
            }

            allUsers.AddRange(usersPage.Select(MapToDomainUser));
            page++;
        }

        return View(allUsers.OrderByDescending(u => u.CreatedAt));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleUserStatus(Guid id)
    {
        var disabledResult = EnsureLegacyAdminEnabled();
        if (disabledResult != null) return disabledResult;

        var (user, getUserError) = await _legacyAdminApiClient.GetUserByIdAsync(id, HttpContext.RequestAborted);
        if (user == null)
        {
            TempData["Error"] = getUserError ?? "Usuario nao encontrado.";
            return RedirectToAction("Users");
        }

        var (success, updateError) = await _legacyAdminApiClient.UpdateUserStatusAsync(
            id,
            !user.IsActive,
            "Atualizacao via painel legado do prestador.",
            HttpContext.RequestAborted);

        if (!success)
        {
            TempData["Error"] = updateError ?? "Nao foi possivel atualizar o status do usuario.";
        }
        else
        {
            TempData["Success"] = user.IsActive
                ? "Usuario bloqueado com sucesso."
                : "Usuario ativado com sucesso.";
        }

        return RedirectToAction("Users");
    }

    private static User MapToDomainUser(ConsertaPraMim.Application.DTOs.AdminUserListItemDto dto)
    {
        var role = Enum.TryParse<UserRole>(dto.Role, true, out var parsedRole)
            ? parsedRole
            : UserRole.Client;

        return new User
        {
            Id = dto.Id,
            Name = dto.Name,
            Email = dto.Email,
            Phone = dto.Phone,
            Role = role,
            IsActive = dto.IsActive,
            CreatedAt = dto.CreatedAt
        };
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
