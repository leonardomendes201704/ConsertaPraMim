using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Web.Provider.Security;
using ConsertaPraMim.Web.Provider.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace ConsertaPraMim.Web.Provider.Controllers;

public class AccountController : Controller
{
    private readonly IProviderAuthApiClient _authApiClient;
    private readonly IProviderOnboardingApiClient _onboardingApiClient;

    public AccountController(IProviderAuthApiClient authApiClient, IProviderOnboardingApiClient onboardingApiClient)
    {
        _authApiClient = authApiClient;
        _onboardingApiClient = onboardingApiClient;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (!User.IsInRole(UserRole.Provider.ToString()))
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                ViewBag.Error = "Sessao invalida para o portal do prestador. Faca login novamente.";
                return View();
            }

            var userIdRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdRaw, out _))
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                ViewBag.Error = "Sessao invalida. Faca login novamente.";
                return View();
            }

            var (onboardingState, onboardingError) = await _onboardingApiClient.GetStateAsync(HttpContext.RequestAborted);
            if (onboardingState == null)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                ViewBag.Error = onboardingError ?? "Sessao expirada ou invalida. Faca login novamente.";
                return View();
            }

            var isOnboardingComplete = onboardingState.IsCompleted || onboardingState.Status == ProviderOnboardingStatus.Active;
            if (!isOnboardingComplete)
            {
                return RedirectToAction("Index", "Onboarding");
            }

            return RedirectToAction("Index", "Home");
        }

        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Login(string email, string password)
    {
        var loginResult = await _authApiClient.LoginAsync(new LoginRequest(email, password));
        var response = loginResult.Response;

        if (response != null && response.Role == UserRole.Provider.ToString())
        {
            await SignInAsync(response);

            var (onboardingState, onboardingError) = await _onboardingApiClient.GetStateAsync(response.Token, HttpContext.RequestAborted);
            if (onboardingState == null)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                ViewBag.Error = onboardingError ?? "Nao foi possivel validar onboarding.";
                return View();
            }

            var isOnboardingComplete = onboardingState.IsCompleted || onboardingState.Status == ProviderOnboardingStatus.Active;
            if (!isOnboardingComplete)
            {
                return RedirectToAction("Index", "Onboarding");
            }

            return RedirectToAction("Index", "Home");
        }

        ViewBag.Error = loginResult.ErrorMessage ?? "Email ou senha invalidos, ou voce nao tem permissao de prestador.";
        return View();
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Register(string name, string email, string password, string phone)
    {
        var normalizedPhone = NormalizePhone(phone);
        var request = new RegisterRequest(name, email, password, normalizedPhone, (int)UserRole.Provider);
        var registerResult = await _authApiClient.RegisterAsync(request);
        var response = registerResult.Response;

        if (response != null)
        {
            await SignInAsync(response);
            return RedirectToAction("Index", "Onboarding");
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

    private async Task SignInAsync(LoginResponse response)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, response.UserName),
            new Claim(ClaimTypes.Email, response.Email),
            new Claim(ClaimTypes.Role, response.Role),
            new Claim(ClaimTypes.NameIdentifier, response.UserId.ToString()),
            new Claim(WebProviderClaimTypes.ApiToken, response.Token)
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
