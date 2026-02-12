using Microsoft.AspNetCore.Mvc;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.DTOs;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Web.Client.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _authService;

    public AccountController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity.IsAuthenticated) return RedirectToAction("Index", "Home");
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(string email, string password)
    {
        var response = await _authService.LoginAsync(new LoginRequest(email, password));
        
        if (response != null && response.Role == UserRole.Client.ToString())
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, response.UserName),
                new Claim(ClaimTypes.Email, response.Email),
                new Claim(ClaimTypes.Role, response.Role),
                new Claim(ClaimTypes.NameIdentifier, response.UserId.ToString())
            };
            
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            return RedirectToAction("Index", "Home");
        }

        ViewBag.Error = "Email ou senha inválidos, ou você não tem permissão de cliente.";
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
        var response = await _authService.RegisterAsync(request);

        if (response != null)
        {
            return RedirectToAction("Login");
        }

        ViewBag.Error = "Erro ao cadastrar. O e-mail pode já estar em uso.";
        return View();
    }

    [HttpGet]
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
