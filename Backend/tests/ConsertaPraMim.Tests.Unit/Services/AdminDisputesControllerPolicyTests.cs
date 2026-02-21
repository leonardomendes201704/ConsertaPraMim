using ConsertaPraMim.API.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminDisputesControllerPolicyTests
{
    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin disputes controller politica | Controller | Deve protected com admin only politica.
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

