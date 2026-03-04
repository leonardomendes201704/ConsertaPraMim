using ConsertaPraMim.Web.TelegramBridge.Controllers;
using ConsertaPraMim.Web.TelegramBridge.Hubs;
using Microsoft.AspNetCore.Authorization;

namespace ConsertaPraMim.Tests.Unit.Services;

public class TelegramBridgeAuthorizationTests
{
    /// <summary>
    /// Cenario: acesso anonimo tenta abrir a tela principal do chat.
    /// Passos: valida por reflexao os atributos de autorizacao do HomeController.
    /// Resultado esperado: controller exige autenticacao para proteger o chat da bridge.
    /// </summary>
    [Fact(DisplayName = "Telegram bridge authorization | Home controller | Deve exigir autenticacao")]
    public void HomeController_ShouldRequireAuthorization()
    {
        var authorize = typeof(HomeController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorize);
    }

    /// <summary>
    /// Cenario: acesso anonimo tenta consumir endpoints REST do chat.
    /// Passos: valida por reflexao os atributos de autorizacao no ChatApiController.
    /// Resultado esperado: endpoints de chat exigem autenticacao.
    /// </summary>
    [Fact(DisplayName = "Telegram bridge authorization | Chat api controller | Deve exigir autenticacao")]
    public void ChatApiController_ShouldRequireAuthorization()
    {
        var authorize = typeof(ChatApiController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorize);
    }

    /// <summary>
    /// Cenario: acesso anonimo tenta conectar no hub SignalR do chat.
    /// Passos: valida por reflexao os atributos de autorizacao do TelegramChatHub.
    /// Resultado esperado: hub exige usuario autenticado.
    /// </summary>
    [Fact(DisplayName = "Telegram bridge authorization | Chat hub | Deve exigir autenticacao")]
    public void TelegramChatHub_ShouldRequireAuthorization()
    {
        var authorize = typeof(TelegramChatHub)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorize);
    }

    /// <summary>
    /// Cenario: cliente sem sessao acessa a tela de login.
    /// Passos: valida por reflexao se as acoes Login permitem acesso anonimo.
    /// Resultado esperado: GET/POST Login estao marcadas com AllowAnonymous.
    /// </summary>
    [Fact(DisplayName = "Telegram bridge authorization | Account controller | Login deve permitir acesso anonimo")]
    public void AccountController_LoginActions_ShouldAllowAnonymous()
    {
        var loginGet = typeof(AccountController).GetMethod(nameof(AccountController.Login), [typeof(string)]);
        var loginPost = typeof(AccountController).GetMethod(nameof(AccountController.Login), [typeof(ConsertaPraMim.Web.TelegramBridge.Models.LoginViewModel), typeof(CancellationToken)]);

        Assert.NotNull(loginGet);
        Assert.NotNull(loginPost);

        var allowAnonymousGet = loginGet!
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true)
            .Cast<AllowAnonymousAttribute>()
            .FirstOrDefault();

        var allowAnonymousPost = loginPost!
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true)
            .Cast<AllowAnonymousAttribute>()
            .FirstOrDefault();

        Assert.NotNull(allowAnonymousGet);
        Assert.NotNull(allowAnonymousPost);
    }
}
