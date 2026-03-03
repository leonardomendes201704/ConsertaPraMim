using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Services;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.Web.TelegramBridge.Controllers;

public sealed class AccountController : Controller
{
    private readonly ITelegramBridgeAuthApiClient _telegramBridgeAuthApiClient;

    public AccountController(ITelegramBridgeAuthApiClient telegramBridgeAuthApiClient)
    {
        _telegramBridgeAuthApiClient = telegramBridgeAuthApiClient;
    }

    [HttpGet]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        return View(new LoginViewModel
        {
            ReturnUrl = returnUrl
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var loginResult = await _telegramBridgeAuthApiClient.LoginAsync(
            model.Email,
            model.Password,
            cancellationToken);

        if (loginResult.Response == null)
        {
            model.ErrorMessage = loginResult.ErrorMessage ?? "Falha ao autenticar com a API.";
            model.Password = string.Empty;
            return View(model);
        }

        if (!loginResult.Response.Role.Equals("Client", StringComparison.OrdinalIgnoreCase))
        {
            model.ErrorMessage = "A conta autenticada nao possui perfil de cliente.";
            model.Password = string.Empty;
            return View(model);
        }

        model.ErrorMessage = "Credenciais validadas na API com sucesso. Persistencia de sessao sera implementada na proxima task.";
        model.Password = string.Empty;
        return View(model);
    }
}
