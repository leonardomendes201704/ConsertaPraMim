using ConsertaPraMim.API.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminDisputesControllerPolicyTests
{
    [Fact]
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

