using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminUsersControllerTests
{
    /// <summary>
    /// Cenario: endpoint de gestao de usuarios exposto apenas para operacao administrativa.
    /// Passos: usa reflexao para ler o atributo de autorizacao do controller.
    /// Resultado esperado: policy AdminOnly obrigatoria para qualquer acesso aos recursos de usuarios.
    /// </summary>
    [Fact(DisplayName = "Admin usuarios controller | Controller | Deve protected com admin only politica")]
    public void Controller_ShouldBeProtectedWithAdminOnlyPolicy()
    {
        var authorize = typeof(AdminUsersController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal("AdminOnly", authorize!.Policy);
    }

    /// <summary>
    /// Cenario: admin consulta detalhes de um usuario que nao existe na base.
    /// Passos: mocka servico retornando null para o ID solicitado e executa GetById.
    /// Resultado esperado: retorno NotFound sem payload, representando ausencia do recurso.
    /// </summary>
    [Fact(DisplayName = "Admin usuarios controller | Obter por id | Deve retornar nao encontrado quando usuario nao exist")]
    public async Task GetById_ShouldReturnNotFound_WhenUserDoesNotExist()
    {
        var serviceMock = new Mock<IAdminUserService>();
        serviceMock.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((AdminUserDetailsDto?)null);
        var controller = new AdminUsersController(serviceMock.Object);

        var result = await controller.GetById(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    /// <summary>
    /// Cenario: alteracao de status e recusada pela regra de negocio (ex.: ultima conta admin ativa).
    /// Passos: autentica ator admin, mocka servico retornando falha de dominio e chama UpdateStatus.
    /// Resultado esperado: resposta Conflict para sinalizar violacao de regra operacional.
    /// </summary>
    [Fact(DisplayName = "Admin usuarios controller | Atualizar status | Deve retornar conflito quando servico rejects")]
    public async Task UpdateStatus_ShouldReturnConflict_WhenServiceRejects()
    {
        var userId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var serviceMock = new Mock<IAdminUserService>();
        serviceMock.Setup(s => s.UpdateStatusAsync(
                userId,
                It.IsAny<AdminUpdateUserStatusRequestDto>(),
                actorId,
                "admin@teste.com"))
            .ReturnsAsync(new AdminUpdateUserStatusResultDto(false, "last_admin_forbidden", "erro"));

        var controller = new AdminUsersController(serviceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, actorId.ToString()),
                        new Claim(ClaimTypes.Email, "admin@teste.com")
                    }))
                }
            }
        };

        var result = await controller.UpdateStatus(userId, new AdminUpdateUserStatusRequestDto(false, "x"));

        Assert.IsType<ConflictObjectResult>(result);
    }

    /// <summary>
    /// Cenario: admin autenticado cria uma nova conta administrativa via endpoint dedicado.
    /// Passos: injeta claims do ator admin, mocka servico retornando sucesso e executa CreateAdmin.
    /// Resultado esperado: retorno CreatedAtAction com payload de sucesso e usuario criado.
    /// </summary>
    [Fact(DisplayName = "Admin usuarios controller | Criar admin | Deve retornar created quando servico succeeds")]
    public async Task CreateAdmin_ShouldReturnCreated_WhenServiceSucceeds()
    {
        var actorId = Guid.NewGuid();
        var createdUserId = Guid.NewGuid();
        var request = new AdminCreateAdminUserRequestDto("Novo Admin", "novo.admin@teste.com", "11999998888", "Senha@123");
        var serviceMock = new Mock<IAdminUserService>();
        serviceMock.Setup(s => s.CreateAdminUserAsync(
                request,
                actorId,
                "admin@teste.com"))
            .ReturnsAsync(new AdminCreateAdminUserResultDto(
                true,
                new AdminUserListItemDto(
                    createdUserId,
                    "Novo Admin",
                    "novo.admin@teste.com",
                    "11999998888",
                    "Admin",
                    true,
                    DateTime.UtcNow)));

        var controller = new AdminUsersController(serviceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, actorId.ToString()),
                        new Claim(ClaimTypes.Email, "admin@teste.com")
                    }))
                }
            }
        };

        var result = await controller.CreateAdmin(request);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(AdminUsersController.GetById), created.ActionName);
        var payload = Assert.IsType<AdminCreateAdminUserResultDto>(created.Value);
        Assert.True(payload.Success);
        Assert.NotNull(payload.User);
        Assert.Equal(createdUserId, payload.User!.Id);
    }

    /// <summary>
    /// Cenario: tentativa de criar admin com email ja existente na base.
    /// Passos: mocka servico retornando erro de conflito e executa CreateAdmin com ator autenticado.
    /// Resultado esperado: endpoint devolve Conflict para sinalizar duplicidade de email.
    /// </summary>
    [Fact(DisplayName = "Admin usuarios controller | Criar admin | Deve retornar conflito quando email exists")]
    public async Task CreateAdmin_ShouldReturnConflict_WhenEmailAlreadyExists()
    {
        var actorId = Guid.NewGuid();
        var request = new AdminCreateAdminUserRequestDto("Novo Admin", "admin@teste.com", "11999998888", "Senha@123");
        var serviceMock = new Mock<IAdminUserService>();
        serviceMock.Setup(s => s.CreateAdminUserAsync(
                request,
                actorId,
                "admin@teste.com"))
            .ReturnsAsync(new AdminCreateAdminUserResultDto(false, ErrorCode: "email_already_exists", ErrorMessage: "duplicado"));

        var controller = new AdminUsersController(serviceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, actorId.ToString()),
                        new Claim(ClaimTypes.Email, "admin@teste.com")
                    }))
                }
            }
        };

        var result = await controller.CreateAdmin(request);

        Assert.IsType<ConflictObjectResult>(result);
    }
}
