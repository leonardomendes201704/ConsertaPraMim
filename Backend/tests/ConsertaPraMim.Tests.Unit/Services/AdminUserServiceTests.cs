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
    private readonly Mock<IAdminAuditLogRepository> _auditRepositoryMock;
    private readonly AdminUserService _service;

    public AdminUserServiceTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _auditRepositoryMock = new Mock<IAdminAuditLogRepository>();
        _service = new AdminUserService(_userRepositoryMock.Object, _auditRepositoryMock.Object);
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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
}
