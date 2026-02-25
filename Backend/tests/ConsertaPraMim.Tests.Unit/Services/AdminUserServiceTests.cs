using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminUserServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IProviderTrustReviewRepository> _providerTrustReviewRepositoryMock;
    private readonly Mock<IAdminAuditLogRepository> _auditRepositoryMock;
    private readonly AdminUserService _service;

    public AdminUserServiceTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _providerTrustReviewRepositoryMock = new Mock<IProviderTrustReviewRepository>();
        _auditRepositoryMock = new Mock<IAdminAuditLogRepository>();
        _service = new AdminUserService(_userRepositoryMock.Object, _providerTrustReviewRepositoryMock.Object, _auditRepositoryMock.Object);
    }

    /// <summary>
    /// Cenario: consulta administrativa de usuarios com filtro combinado por texto, papel e status ativo.
    /// Passos: prepara massa com perfis distintos e executa GetUsersAsync com pagina e filtros especificos.
    /// Resultado esperado: apenas usuarios que satisfazem os criterios retornam, respeitando paginação.
    /// </summary>
    [Fact(DisplayName = "Admin usuario servico | Obter usuarios | Deve filter e paginate")]
    public async Task GetUsersAsync_ShouldFilterAndPaginate()
    {
        _userRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>
        {
            new() { Id = Guid.NewGuid(), Name = "Admin A", Email = "admin@teste.com", Phone = "111", Role = UserRole.Admin, IsActive = true, CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
            new() { Id = Guid.NewGuid(), Name = "Provider B", Email = "provider@teste.com", Phone = "222", Role = UserRole.Provider, IsActive = true, CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new() { Id = Guid.NewGuid(), Name = "Provider C", Email = "provider2@teste.com", Phone = "333", Role = UserRole.Provider, IsActive = false, CreatedAt = DateTime.UtcNow.AddMinutes(-3) }
        });

        var result = await _service.GetUsersAsync(new AdminUsersQueryDto("provider", "Provider", true, 1, 10));

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("Provider", result.Items[0].Role);
        Assert.True(result.Items[0].IsActive);
    }

    /// <summary>
    /// Cenario: tentativa de desativar o ultimo admin ativo da plataforma.
    /// Passos: mocka repositorio com apenas um admin ativo e solicita desativacao desse mesmo usuario.
    /// Resultado esperado: operacao negada com erro last_admin_forbidden e sem persistencia/auditoria.
    /// </summary>
    [Fact(DisplayName = "Admin usuario servico | Atualizar status | Deve falhar quando deactivating last active admin")]
    public async Task UpdateStatusAsync_ShouldFail_WhenDeactivatingLastActiveAdmin()
    {
        var adminId = Guid.NewGuid();
        var targetAdmin = new User { Id = adminId, Role = UserRole.Admin, IsActive = true };

        _userRepositoryMock.Setup(r => r.GetByIdAsync(adminId)).ReturnsAsync(targetAdmin);
        _userRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>
        {
            new() { Id = adminId, Role = UserRole.Admin, IsActive = true }
        });

        var result = await _service.UpdateStatusAsync(
            adminId,
            new AdminUpdateUserStatusRequestDto(false, "maintenance"),
            Guid.NewGuid(),
            "actor@teste.com");

        Assert.False(result.Success);
        Assert.Equal("last_admin_forbidden", result.ErrorCode);
        _userRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
        _auditRepositoryMock.Verify(r => r.AddAsync(It.IsAny<AdminAuditLog>()), Times.Never);
    }

    /// <summary>
    /// Cenario: mudanca de status valida para usuario nao-admin.
    /// Passos: mocka alvo existente, executa UpdateStatusAsync e inspeciona chamadas de update e audit log.
    /// Resultado esperado: status atualizado, sucesso retornado e auditoria contendo before/after da alteracao.
    /// </summary>
    [Fact(DisplayName = "Admin usuario servico | Atualizar status | Deve atualizar e audit quando valido")]
    public async Task UpdateStatusAsync_ShouldUpdateAndAudit_WhenValid()
    {
        var targetId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var targetUser = new User { Id = targetId, Role = UserRole.Provider, IsActive = true };

        _userRepositoryMock.Setup(r => r.GetByIdAsync(targetId)).ReturnsAsync(targetUser);
        _userRepositoryMock.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

        var result = await _service.UpdateStatusAsync(
            targetId,
            new AdminUpdateUserStatusRequestDto(false, "policy"),
            actorId,
            "admin@teste.com");

        Assert.True(result.Success);
        _userRepositoryMock.Verify(r => r.UpdateAsync(It.Is<User>(u => u.Id == targetId && !u.IsActive)), Times.Once);
        _auditRepositoryMock.Verify(r => r.AddAsync(It.Is<AdminAuditLog>(a =>
            a.ActorUserId == actorId &&
            a.ActorEmail == "admin@teste.com" &&
            a.TargetId == targetId &&
            a.Action == "UserStatusChanged" &&
            !string.IsNullOrWhiteSpace(a.Metadata) &&
            a.Metadata!.Contains("\"before\"") &&
            a.Metadata.Contains("\"after\""))), Times.Once);
    }

    /// <summary>
    /// Cenario: criacao valida de novo operador admin pela equipe administrativa.
    /// Passos: valida inexistencia de email, executa CreateAdminUserAsync e inspeciona persistencia/auditoria.
    /// Resultado esperado: usuario admin ativo criado com hash de senha, telefone normalizado e trilha de auditoria.
    /// </summary>
    [Fact(DisplayName = "Admin usuario servico | Criar admin | Deve criar usuario e audit quando payload valido")]
    public async Task CreateAdminUserAsync_ShouldCreateAndAudit_WhenPayloadIsValid()
    {
        var actorId = Guid.NewGuid();
        User? persistedUser = null;

        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync("novo.admin@teste.com"))
            .ReturnsAsync((User?)null);
        _userRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<User>()))
            .ReturnsAsync((User user) =>
            {
                persistedUser = user;
                return user;
            });

        var result = await _service.CreateAdminUserAsync(
            new AdminCreateAdminUserRequestDto(
                "Novo Admin",
                "novo.admin@teste.com",
                "(11) 99999-1234",
                "Senha@123"),
            actorId,
            "ator@teste.com");

        Assert.True(result.Success);
        Assert.NotNull(result.User);
        Assert.Equal("Admin", result.User!.Role);
        Assert.NotNull(persistedUser);
        Assert.Equal(UserRole.Admin, persistedUser!.Role);
        Assert.Equal("11999991234", persistedUser.Phone);
        Assert.True(BCrypt.Net.BCrypt.Verify("Senha@123", persistedUser.PasswordHash));
        var persistedUserId = persistedUser.Id;

        _auditRepositoryMock.Verify(r => r.AddAsync(It.Is<AdminAuditLog>(a =>
            a.ActorUserId == actorId &&
            a.ActorEmail == "ator@teste.com" &&
            a.TargetId == persistedUserId &&
            a.Action == "AdminUserCreated" &&
            !string.IsNullOrWhiteSpace(a.Metadata))), Times.Once);
    }

    /// <summary>
    /// Cenario: tentativa de criar admin com e-mail ja utilizado.
    /// Passos: mocka repositorio retornando usuario existente para o email informado e executa CreateAdminUserAsync.
    /// Resultado esperado: operacao negada com erro de conflito sem persistir novo usuario.
    /// </summary>
    [Fact(DisplayName = "Admin usuario servico | Criar admin | Deve falhar quando email ja existe")]
    public async Task CreateAdminUserAsync_ShouldFail_WhenEmailAlreadyExists()
    {
        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync("admin@teste.com"))
            .ReturnsAsync(new User
            {
                Id = Guid.NewGuid(),
                Email = "admin@teste.com",
                Role = UserRole.Admin
            });

        var result = await _service.CreateAdminUserAsync(
            new AdminCreateAdminUserRequestDto(
                "Admin Duplicado",
                "admin@teste.com",
                "11999999999",
                "Senha@123"),
            Guid.NewGuid(),
            "ator@teste.com");

        Assert.False(result.Success);
        Assert.Equal("email_already_exists", result.ErrorCode);
        _userRepositoryMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
        _auditRepositoryMock.Verify(r => r.AddAsync(It.IsAny<AdminAuditLog>()), Times.Never);
    }
}
