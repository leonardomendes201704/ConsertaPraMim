using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Web.Client.Security;
using ConsertaPraMim.Web.Client.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ConsertaPraMim.Web.Client.Controllers;

public class AccountController : Controller
{
    private readonly IClientAuthApiClient _authApiClient;

    public AccountController(IClientAuthApiClient authApiClient)
    {
        _authApiClient = authApiClient;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
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
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, response.UserName),
                new Claim(ClaimTypes.Email, response.Email),
                new Claim(ClaimTypes.Role, response.Role),
                new Claim(ClaimTypes.NameIdentifier, response.UserId.ToString()),
                new Claim(WebClientClaimTypes.ApiToken, response.Token)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            return RedirectToAction("Index", "Home");
        }

        ViewBag.Error = loginResult.ErrorMessage ?? "Email ou senha invalidos, ou voce nao tem permissao de cliente.";
        return View();
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(string name, string email, string password, string phone)
    {
        var request = new RegisterRequest(name, email, password, phone, (int)UserRole.Client);
        var registerResult = await _authApiClient.RegisterAsync(request);

        if (registerResult.Response != null)
        {
            return RedirectToAction("Login");
        }

        ViewBag.Error = registerResult.ErrorMessage ?? "Erro ao cadastrar. O e-mail pode ja estar em uso.";
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
}

