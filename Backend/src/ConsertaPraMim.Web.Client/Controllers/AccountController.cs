using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Web.Client.Security;
using ConsertaPraMim.Web.Client.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace ConsertaPraMim.Web.Client.Controllers;

public class AccountController : Controller
{
    private readonly IClientAuthApiClient _authApiClient;

    public AccountController(IClientAuthApiClient authApiClient)
    {
        _authApiClient = authApiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        var hasForcedReauthentication = !string.IsNullOrWhiteSpace(returnUrl);
        var hasApiToken = !string.IsNullOrWhiteSpace(User.FindFirst(WebClientClaimTypes.ApiToken)?.Value);

        if (User.Identity?.IsAuthenticated == true)
        {
            if (hasForcedReauthentication || !hasApiToken)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                ViewBag.Error = "Sessao expirada. Faca login novamente.";
                return View();
            }

            return RedirectToAction("Index", "Home");
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(string email, string password)
    {
        var loginResult = await _authApiClient.LoginAsync(new LoginRequest(email, password));
        var response = loginResult.Response;

        if (response != null && response.Role == UserRole.Client.ToString())
        {
            await SignInAsync(response);
            return RedirectToAction("Index", "Home");
        }

        ViewBag.Error = loginResult.ErrorMessage ?? "Email ou senha invalidos, ou voce nao tem permissao de cliente.";
        return View();
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(string name, string email, string password, string phone)
    {
        var normalizedPhone = NormalizePhone(phone);
        var request = new RegisterRequest(name, email, password, normalizedPhone, (int)UserRole.Client);
        var registerResult = await _authApiClient.RegisterAsync(request);
        var response = registerResult.Response;

        if (response != null && response.Role == UserRole.Client.ToString())
        {
            await SignInAsync(response);
            return RedirectToAction(nameof(RegisterSuccess));
        }

        ViewBag.Error = registerResult.ErrorMessage ?? "Erro ao cadastrar. O e-mail pode ja estar em uso.";
        return View();
    }

    [HttpGet]
    public IActionResult RegisterSuccess()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction(nameof(Login));
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private async Task SignInAsync(LoginResponse response)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, response.UserName),
            new Claim(ClaimTypes.Email, response.Email),
            new Claim(ClaimTypes.Role, response.Role),
            new Claim(ClaimTypes.NameIdentifier, response.UserId.ToString()),
            new Claim(WebClientClaimTypes.ApiToken, response.Token)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity));
    }

    private static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return string.Empty;
        }

        return Regex.Replace(phone, @"\D", string.Empty);
    }
}
