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

    [Fact]
    public async Task CreateAsync_ShouldReturnDuplicateName_WhenNameAlreadyExists()
    {
        // Arrange
        var actorId = Guid.NewGuid();
        var request = new AdminCreateServiceCategoryRequestDto(
            Name: "Eletrica",
            Slug: "eletrica",
            LegacyCategory: "Electrical");

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

    [Fact]
    public async Task CreateAsync_ShouldPersistCategoryAndAudit_WhenValid()
    {
        // Arrange
        var actorId = Guid.NewGuid();
        var request = new AdminCreateServiceCategoryRequestDto(
            Name: "Automacao Residencial",
            Slug: null,
            LegacyCategory: "Other");

        _categoryRepositoryMock.Setup(r => r.GetByNameAsync("Automacao Residencial")).ReturnsAsync((ServiceCategoryDefinition?)null);
        _categoryRepositoryMock.Setup(r => r.GetBySlugAsync("automacao-residencial")).ReturnsAsync((ServiceCategoryDefinition?)null);

        // Act
        var result = await _service.CreateAsync(request, actorId, "admin@teste.com");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Category);
        Assert.Equal("Automacao Residencial", result.Category!.Name);
        Assert.Equal("automacao-residencial", result.Category.Slug);
        _categoryRepositoryMock.Verify(r => r.AddAsync(It.IsAny<ServiceCategoryDefinition>()), Times.Once);
        _auditRepositoryMock.Verify(r => r.AddAsync(It.Is<AdminAuditLog>(a =>
            a.ActorUserId == actorId &&
            a.Action == "ServiceCategoryCreated" &&
            a.TargetType == "ServiceCategory")), Times.Once);
    }

    [Fact]
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

    [Fact]
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
