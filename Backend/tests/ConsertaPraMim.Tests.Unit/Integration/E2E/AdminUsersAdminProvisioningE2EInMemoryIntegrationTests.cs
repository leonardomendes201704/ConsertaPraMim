using System.Security.Claims;
using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConsertaPraMim.Tests.Unit.Integration.E2E;

public class AdminUsersAdminProvisioningE2EInMemoryIntegrationTests
{
    /// <summary>
    /// Cenario: admin autenticado cria nova conta administrativa e consulta o recurso criado.
    /// Passos: cria ator admin em memoria, chama endpoint CreateAdmin e consulta GetById do usuario criado.
    /// Resultado esperado: conta criada com role admin ativa, senha hash persistida e auditoria registrada.
    /// </summary>
    [Fact(DisplayName = "Admin usuarios admin provisioning e 2 e em memory integracao | Criar admin | Deve provision account end para end")]
    public async Task CreateAdmin_ShouldProvisionAccount_EndToEnd()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var actorAdmin = CreateUser("ator.admin@teste.com", UserRole.Admin);
        context.Users.Add(actorAdmin);
        await context.SaveChangesAsync();

        var service = new AdminUserService(
            new UserRepository(context),
            new ProviderTrustReviewRepository(context),
            new AdminAuditLogRepository(context));
        var controller = BuildController(service, actorAdmin);

        var createResult = await controller.CreateAdmin(new AdminCreateAdminUserRequestDto(
            "Novo Admin E2E",
            "novo.admin.e2e@teste.com",
            "11999888777",
            "Senha@123"));

        var created = Assert.IsType<CreatedAtActionResult>(createResult);
        var payload = Assert.IsType<AdminCreateAdminUserResultDto>(created.Value);
        Assert.True(payload.Success);
        Assert.NotNull(payload.User);
        Assert.Equal("Admin", payload.User!.Role);
        Assert.True(payload.User.IsActive);

        var persistedUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "novo.admin.e2e@teste.com");
        Assert.NotNull(persistedUser);
        Assert.Equal(UserRole.Admin, persistedUser!.Role);
        Assert.True(persistedUser.IsActive);
        Assert.True(BCrypt.Net.BCrypt.Verify("Senha@123", persistedUser.PasswordHash));

        var audit = await context.AdminAuditLogs.FirstOrDefaultAsync(a =>
            a.Action == "AdminUserCreated" &&
            a.TargetId == persistedUser.Id &&
            a.ActorUserId == actorAdmin.Id);
        Assert.NotNull(audit);

        var detailsResult = await controller.GetById(persistedUser.Id);
        var detailsOk = Assert.IsType<OkObjectResult>(detailsResult);
        var details = Assert.IsType<AdminUserDetailsDto>(detailsOk.Value);
        Assert.Equal("Admin", details.Role);
        Assert.Equal("novo.admin.e2e@teste.com", details.Email);
    }

    /// <summary>
    /// Cenario: tentativa de criar admin com e-mail ja existente na base.
    /// Passos: seed de admin existente, chamada do endpoint CreateAdmin com mesmo email.
    /// Resultado esperado: API retorna Conflict e nenhum novo usuario admin e persistido.
    /// </summary>
    [Fact(DisplayName = "Admin usuarios admin provisioning e 2 e em memory integracao | Criar admin | Deve return conflict quando email duplicado")]
    public async Task CreateAdmin_ShouldReturnConflict_WhenEmailAlreadyExists()
    {
        await using var context = InfrastructureTestDbContextFactory.CreateInMemoryContext();
        var actorAdmin = CreateUser("ator.admin@teste.com", UserRole.Admin);
        var existingAdmin = CreateUser("admin.existente@teste.com", UserRole.Admin);
        context.Users.AddRange(actorAdmin, existingAdmin);
        await context.SaveChangesAsync();

        var service = new AdminUserService(
            new UserRepository(context),
            new ProviderTrustReviewRepository(context),
            new AdminAuditLogRepository(context));
        var controller = BuildController(service, actorAdmin);

        var result = await controller.CreateAdmin(new AdminCreateAdminUserRequestDto(
            "Admin Duplicado",
            "admin.existente@teste.com",
            "11999888776",
            "Senha@123"));

        Assert.IsType<ConflictObjectResult>(result);

        var adminsWithEmail = await context.Users.CountAsync(u => u.Email == "admin.existente@teste.com");
        Assert.Equal(1, adminsWithEmail);
    }

    private static AdminUsersController BuildController(AdminUserService service, User actorAdmin)
    {
        var controller = new AdminUsersController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, actorAdmin.Id.ToString()),
            new Claim(ClaimTypes.Email, actorAdmin.Email),
            new Claim(ClaimTypes.Role, UserRole.Admin.ToString())
        ], "TestAuth"));

        return controller;
    }

    private static User CreateUser(string email, UserRole role)
    {
        return new User
        {
            Name = email.Split('@')[0],
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Senha@123"),
            Phone = "11999999999",
            Role = role,
            IsActive = true
        };
    }
}
