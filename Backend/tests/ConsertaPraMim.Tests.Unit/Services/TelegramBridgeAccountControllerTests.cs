using System.Security.Claims;
using ConsertaPraMim.Web.TelegramBridge.Controllers;
using ConsertaPraMim.Web.TelegramBridge.Models;
using ConsertaPraMim.Web.TelegramBridge.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Routing;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class TelegramBridgeAccountControllerTests
{
    /// <summary>
    /// Cenario: cliente informa credenciais validas na tela de login do Telegram Bridge.
    /// Passos: API de autenticacao retorna perfil Client com token e controller executa SignInAsync.
    /// Resultado esperado: redirecionamento para Home/Index e sessao de autenticacao criada.
    /// </summary>
    [Fact(DisplayName = "Telegram bridge account controller | Login | Deve autenticar e redirecionar quando credenciais validas")]
    public async Task Login_ShouldSignInAndRedirect_WhenCredentialsAreValid()
    {
        var authApiClientMock = new Mock<ITelegramBridgeAuthApiClient>();
        authApiClientMock
            .Setup(client => client.LoginAsync("cliente@teste.com", "SenhaForte123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new TelegramBridgeLoginResponse
            {
                UserId = Guid.NewGuid(),
                Token = "token-api-valido",
                UserName = "Cliente Teste",
                Role = "Client",
                Email = "cliente@teste.com"
            }, (string?)null));

        var authenticationServiceMock = new Mock<IAuthenticationService>();
        ClaimsPrincipal? signedPrincipal = null;
        AuthenticationProperties? signedProperties = null;

        authenticationServiceMock
            .Setup(service => service.SignInAsync(
                It.IsAny<HttpContext>(),
                It.IsAny<string>(),
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<AuthenticationProperties?>()))
            .Callback<HttpContext, string, ClaimsPrincipal, AuthenticationProperties?>((_, _, principal, properties) =>
            {
                signedPrincipal = principal;
                signedProperties = properties;
            })
            .Returns(Task.CompletedTask);

        var controller = CreateController(authApiClientMock.Object, authenticationServiceMock.Object);
        var model = new LoginViewModel
        {
            Email = "cliente@teste.com",
            Password = "SenhaForte123"
        };

        var result = await controller.Login(model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);

        authenticationServiceMock.Verify(service => service.SignInAsync(
                It.IsAny<HttpContext>(),
                CookieAuthenticationDefaults.AuthenticationScheme,
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<AuthenticationProperties?>()),
            Times.Once);

        Assert.NotNull(signedPrincipal);
        Assert.Equal("Client", signedPrincipal!.FindFirstValue(ClaimTypes.Role));
        Assert.Equal("cliente@teste.com", signedPrincipal.FindFirstValue(ClaimTypes.Email));
        Assert.NotNull(signedProperties);
    }

    /// <summary>
    /// Cenario: cliente informa senha invalida no login da bridge.
    /// Passos: API de autenticacao retorna falha sem payload de usuario.
    /// Resultado esperado: controller retorna View com mensagem de erro e sem criar sessao.
    /// </summary>
    [Fact(DisplayName = "Telegram bridge account controller | Login | Deve retornar erro quando credencial invalida")]
    public async Task Login_ShouldReturnViewWithError_WhenCredentialsAreInvalid()
    {
        var authApiClientMock = new Mock<ITelegramBridgeAuthApiClient>();
        authApiClientMock
            .Setup(client => client.LoginAsync("cliente@teste.com", "senha-invalida", It.IsAny<CancellationToken>()))
            .ReturnsAsync(((TelegramBridgeLoginResponse?)null, "Email ou senha invalidos."));

        var authenticationServiceMock = new Mock<IAuthenticationService>();
        var controller = CreateController(authApiClientMock.Object, authenticationServiceMock.Object);

        var model = new LoginViewModel
        {
            Email = "cliente@teste.com",
            Password = "senha-invalida"
        };

        var result = await controller.Login(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var resultModel = Assert.IsType<LoginViewModel>(view.Model);
        Assert.Equal("Email ou senha invalidos.", resultModel.ErrorMessage);
        Assert.Equal(string.Empty, resultModel.Password);

        authenticationServiceMock.Verify(service => service.SignInAsync(
                It.IsAny<HttpContext>(),
                It.IsAny<string>(),
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<AuthenticationProperties?>()),
            Times.Never);
    }

    private static AccountController CreateController(
        ITelegramBridgeAuthApiClient authApiClient,
        IAuthenticationService authenticationService)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRouting();
        services.AddControllersWithViews();
        services.AddSingleton(authenticationService);

        var serviceProvider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };

        var controller = new AccountController(authApiClient)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext,
                RouteData = new RouteData(),
                ActionDescriptor = new ControllerActionDescriptor()
            }
        };

        return controller;
    }
}
