using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminServiceCategoryServiceTests
{
    private readonly Mock<IServiceCategoryRepository> _categoryRepositoryMock;
    private readonly Mock<IAdminAuditLogRepository> _auditRepositoryMock;
    private readonly AdminServiceCategoryService _service;

    public AdminServiceCategoryServiceTests()
    {
        _categoryRepositoryMock = new Mock<IServiceCategoryRepository>();
        _auditRepositoryMock = new Mock<IAdminAuditLogRepository>();

        _service = new AdminServiceCategoryService(
            _categoryRepositoryMock.Object,
            _auditRepositoryMock.Object);
    }

    /// <summary>
    /// Cenario: admin tenta cadastrar categoria com nome ja utilizado no catalogo.
    /// Passos: repositório retorna categoria existente para o mesmo nome e o fluxo de CreateAsync eh executado.
    /// Resultado esperado: falha de negocio com errorCode duplicate_name, sem persistencia e sem auditoria.
    /// </summary>
    [Fact(DisplayName = "Admin servico category servico | Criar | Deve retornar duplicate name quando name already existe")]
    public async Task CreateAsync_ShouldReturnDuplicateName_WhenNameAlreadyExists()
    {
        // Arrange
        var actorId = Guid.NewGuid();
        var request = new AdminCreateServiceCategoryRequestDto(
            Name: "Eletrica",
            Slug: "eletrica",
            LegacyCategory: "Electrical",
            Icon: "bolt");

        _categoryRepositoryMock
            .Setup(r => r.GetByNameAsync("Eletrica"))
            .ReturnsAsync(new ServiceCategoryDefinition { Name = "Eletrica", Slug = "eletrica", LegacyCategory = ServiceCategory.Electrical });

        // Act
        var result = await _service.CreateAsync(request, actorId, "admin@teste.com");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("duplicate_name", result.ErrorCode);
        _categoryRepositoryMock.Verify(r => r.AddAsync(It.IsAny<ServiceCategoryDefinition>()), Times.Never);
        _auditRepositoryMock.Verify(r => r.AddAsync(It.IsAny<AdminAuditLog>()), Times.Never);
    }

    /// <summary>
    /// Cenario: admin cria nova categoria valida sem conflito de nome/slug.
    /// Passos: request chega com slug nulo, servico normaliza slug automaticamente e prossegue com gravacao.
    /// Resultado esperado: categoria criada com dados consistentes e registro de audit trail do evento de criacao.
    /// </summary>
    [Fact(DisplayName = "Admin servico category servico | Criar | Deve persistir category e audit quando valido")]
    public async Task CreateAsync_ShouldPersistCategoryAndAudit_WhenValid()
    {
        // Arrange
        var actorId = Guid.NewGuid();
        var request = new AdminCreateServiceCategoryRequestDto(
            Name: "Automacao Residencial",
            Slug: null,
            LegacyCategory: "Other",
            Icon: "build_circle");

        _categoryRepositoryMock.Setup(r => r.GetByNameAsync("Automacao Residencial")).ReturnsAsync((ServiceCategoryDefinition?)null);
        _categoryRepositoryMock.Setup(r => r.GetBySlugAsync("automacao-residencial")).ReturnsAsync((ServiceCategoryDefinition?)null);

        // Act
        var result = await _service.CreateAsync(request, actorId, "admin@teste.com");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Category);
        Assert.Equal("Automacao Residencial", result.Category!.Name);
        Assert.Equal("automacao-residencial", result.Category.Slug);
        Assert.Equal("build_circle", result.Category.Icon);
        _categoryRepositoryMock.Verify(r => r.AddAsync(It.IsAny<ServiceCategoryDefinition>()), Times.Once);
        _auditRepositoryMock.Verify(r => r.AddAsync(It.Is<AdminAuditLog>(a =>
            a.ActorUserId == actorId &&
            a.Action == "ServiceCategoryCreated" &&
            a.TargetType == "ServiceCategory")), Times.Once);
    }

    /// <summary>
    /// Cenario: tentativa de inativar a unica categoria ativa do sistema.
    /// Passos: consulta de categorias ativas retorna somente a categoria alvo e o admin solicita desativacao.
    /// Resultado esperado: operacao rejeitada com last_active_forbidden, preservando cobertura minima do catalogo.
    /// </summary>
    [Fact(DisplayName = "Admin servico category servico | Atualizar status | Deve reject inactivation quando category last active")]
    public async Task UpdateStatusAsync_ShouldRejectInactivation_WhenCategoryIsLastActive()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        _categoryRepositoryMock
            .Setup(r => r.GetByIdAsync(categoryId))
            .ReturnsAsync(new ServiceCategoryDefinition
            {
                Id = categoryId,
                Name = "Eletrica",
                Slug = "eletrica",
                LegacyCategory = ServiceCategory.Electrical,
                IsActive = true
            });
        _categoryRepositoryMock
            .Setup(r => r.GetActiveAsync())
            .ReturnsAsync(new List<ServiceCategoryDefinition>
            {
                new() { Id = categoryId, Name = "Eletrica", Slug = "eletrica", LegacyCategory = ServiceCategory.Electrical, IsActive = true }
            });

        // Act
        var result = await _service.UpdateStatusAsync(
            categoryId,
            new AdminUpdateServiceCategoryStatusRequestDto(IsActive: false, Reason: "Teste"),
            Guid.NewGuid(),
            "admin@teste.com");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("last_active_forbidden", result.ErrorCode);
        _categoryRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<ServiceCategoryDefinition>()), Times.Never);
        _auditRepositoryMock.Verify(r => r.AddAsync(It.IsAny<AdminAuditLog>()), Times.Never);
    }

    /// <summary>
    /// Cenario: admin inativa categoria quando existem outras categorias ativas em operacao.
    /// Passos: servico valida regra de continuidade, altera status para inativo e atualiza metadados da entidade.
    /// Resultado esperado: inativacao concluida com sucesso e auditoria registrada para rastreabilidade administrativa.
    /// </summary>
    [Fact(DisplayName = "Admin servico category servico | Atualizar status | Deve inactivate e audit quando there other active categories")]
    public async Task UpdateStatusAsync_ShouldInactivateAndAudit_WhenThereAreOtherActiveCategories()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        _categoryRepositoryMock
            .Setup(r => r.GetByIdAsync(categoryId))
            .ReturnsAsync(new ServiceCategoryDefinition
            {
                Id = categoryId,
                Name = "Eletrica",
                Slug = "eletrica",
                LegacyCategory = ServiceCategory.Electrical,
                IsActive = true
            });
        _categoryRepositoryMock
            .Setup(r => r.GetActiveAsync())
            .ReturnsAsync(new List<ServiceCategoryDefinition>
            {
                new() { Id = categoryId, Name = "Eletrica", Slug = "eletrica", LegacyCategory = ServiceCategory.Electrical, IsActive = true },
                new() { Id = Guid.NewGuid(), Name = "Hidraulica", Slug = "hidraulica", LegacyCategory = ServiceCategory.Plumbing, IsActive = true }
            });

        // Act
        var result = await _service.UpdateStatusAsync(
            categoryId,
            new AdminUpdateServiceCategoryStatusRequestDto(IsActive: false, Reason: "Descontinuada"),
            Guid.NewGuid(),
            "admin@teste.com");

        // Assert
        Assert.True(result.Success);
        _categoryRepositoryMock.Verify(r => r.UpdateAsync(It.Is<ServiceCategoryDefinition>(c =>
            c.Id == categoryId &&
            c.IsActive == false &&
            c.UpdatedAt.HasValue)), Times.Once);
        _auditRepositoryMock.Verify(r => r.AddAsync(It.Is<AdminAuditLog>(a =>
            a.Action == "ServiceCategoryStatusChanged" &&
            a.TargetId == categoryId)), Times.Once);
    }
}
