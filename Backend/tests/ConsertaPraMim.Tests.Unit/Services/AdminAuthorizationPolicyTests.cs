using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminAuthorizationPolicyTests
{
    /// <summary>
    /// Cenario: usuario sem perfil admin tenta acessar recurso protegido pela policy AdminOnly.
    /// Passos: cria ClaimsPrincipal com role Provider e executa AuthorizeAsync contra a policy.
    /// Resultado esperado: autorizacao negada para garantir segregacao de acesso administrativo.
    /// </summary>
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

    /// <summary>
    /// Cenario: usuario com role Admin solicita acesso a recurso com policy AdminOnly.
    /// Passos: monta principal autenticado com role correta e avalia a mesma policy via IAuthorizationService.
    /// Resultado esperado: autorizacao concedida, confirmando configuracao correta do controle de acesso.
    /// </summary>
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
