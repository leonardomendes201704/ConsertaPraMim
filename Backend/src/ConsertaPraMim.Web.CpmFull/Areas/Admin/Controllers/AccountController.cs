using System.Security.Claims;
using AppMobileCPM.Areas.Admin.ViewModels;
using AppMobileCPM.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppMobileCPM.Areas.Admin.Controllers;

[Area("Admin")]
[Route("admin")]
public sealed class AccountController : Controller
{
    private readonly IAdminAuthService _adminAuthService;

    public AccountController(IAdminAuthService adminAuthService)
    {
        _adminAuthService = adminAuthService;
    }

    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }

        return View(new AdminLoginInputModel());
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(AdminLoginInputModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = _adminAuthService.ValidateCredentials(model.Username, model.Password);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Usuario ou senha invalidos.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.GivenName, user.DisplayName),
            new(ClaimTypes.Role, AdminAuthConstants.AdminRole)
        };

        var identity = new ClaimsIdentity(claims, AdminAuthConstants.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            AdminAuthConstants.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                AllowRefresh = true,
                ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(12)
            });

        return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
    }

    [Authorize(AuthenticationSchemes = AdminAuthConstants.AuthenticationScheme)]
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AdminAuthConstants.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [Authorize(AuthenticationSchemes = AdminAuthConstants.AuthenticationScheme)]
    [HttpGet("alterar-senha")]
    public IActionResult ChangePassword()
    {
        return View(new AdminChangePasswordInputModel());
    }

    [Authorize(AuthenticationSchemes = AdminAuthConstants.AuthenticationScheme)]
    [HttpPost("alterar-senha")]
    [ValidateAntiForgeryToken]
    public IActionResult ChangePassword(AdminChangePasswordInputModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idClaim, out var userId))
        {
            ModelState.AddModelError(string.Empty, "Sessao invalida. Faca login novamente.");
            return View(model);
        }

        if (!_adminAuthService.ChangePassword(userId, model.CurrentPassword, model.NewPassword, out var message))
        {
            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }

        TempData["AdminSuccessMessage"] = message;
        return RedirectToAction(nameof(ChangePassword));
    }
}
