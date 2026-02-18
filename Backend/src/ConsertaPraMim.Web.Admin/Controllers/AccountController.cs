using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Web.Admin.Services;
using ConsertaPraMim.Web.Admin.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ConsertaPraMim.Web.Admin.Controllers;

public class AccountController : Controller
{
    private readonly IAdminAuthApiClient _authApiClient;

    public AccountController(IAdminAuthApiClient authApiClient)
    {
        _authApiClient = authApiClient;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "AdminHome");
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(string email, string password)
    {
        var loginResult = await _authApiClient.LoginAsync(new LoginRequest(email, password));
        var response = loginResult.Data;
        if (response != null && response.Role == UserRole.Admin.ToString())
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, response.UserName),
                new Claim(ClaimTypes.Email, response.Email),
                new Claim(ClaimTypes.Role, response.Role),
                new Claim(ClaimTypes.NameIdentifier, response.UserId.ToString()),
                new Claim(AdminClaimTypes.ApiToken, response.Token)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            return RedirectToAction("Index", "AdminHome");
        }

        ViewBag.Error = loginResult.ErrorMessage ?? "Email ou senha invalidos, ou voce nao tem permissao de administrador.";
        return View();
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }
}
