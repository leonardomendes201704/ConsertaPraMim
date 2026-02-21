using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminAuthorizationPolicyTests
{
    [Fact(DisplayName = "Admin authorization politica | Admin only politica | Deve reject non admin usuario")]
    public async Task AdminOnlyPolicy_ShouldReject_NonAdminUser()
    {
        await using var serviceProvider = BuildAuthorizationServiceProvider();
        var authorizationService = serviceProvider.GetRequiredService<IAuthorizationService>();
        var nonAdminUser = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Email, "provider@teste.com"),
                new Claim(ClaimTypes.Role, "Provider")
            },
            authenticationType: "TestAuth"));

        var result = await authorizationService.AuthorizeAsync(nonAdminUser, resource: null, policyName: "AdminOnly");

        Assert.False(result.Succeeded);
    }

    [Fact(DisplayName = "Admin authorization politica | Admin only politica | Deve allow admin usuario")]
    public async Task AdminOnlyPolicy_ShouldAllow_AdminUser()
    {
        await using var serviceProvider = BuildAuthorizationServiceProvider();
        var authorizationService = serviceProvider.GetRequiredService<IAuthorizationService>();
        var adminUser = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Email, "admin@teste.com"),
                new Claim(ClaimTypes.Role, "Admin")
            },
            authenticationType: "TestAuth"));

        var result = await authorizationService.AuthorizeAsync(adminUser, resource: null, policyName: "AdminOnly");

        Assert.True(result.Succeeded);
    }

    private static ServiceProvider BuildAuthorizationServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
        });

        return services.BuildServiceProvider();
    }
}
