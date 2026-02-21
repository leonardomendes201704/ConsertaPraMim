using ConsertaPraMim.API.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminDisputesControllerPolicyTests
{
    /// <summary>
    /// Cenario: validacao de seguranca no controller de disputas administrativas.
    /// Passos: inspeciona atributos do AdminDisputesController em runtime para identificar politica de autorizacao aplicada.
    /// Resultado esperado: classe exige policy AdminOnly, impedindo acesso por usuarios sem perfil administrativo.
    /// </summary>
    [Fact(DisplayName = "Admin disputes controller politica | Controller | Deve protected com admin only politica")]
    public void Controller_ShouldBeProtectedWithAdminOnlyPolicy()
    {
        var authorize = typeof(AdminDisputesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("AdminOnly", authorize!.Policy);
    }
}

