using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class AdminDashboardServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IServiceRequestRepository> _serviceRequestRepositoryMock;
    private readonly Mock<IProposalRepository> _proposalRepositoryMock;
    private readonly Mock<IChatMessageRepository> _chatMessageRepositoryMock;
    private readonly AdminDashboardService _service;

    public AdminDashboardServiceTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _serviceRequestRepositoryMock = new Mock<IServiceRequestRepository>();
        _proposalRepositoryMock = new Mock<IProposalRepository>();
        _chatMessageRepositoryMock = new Mock<IChatMessageRepository>();

        _service = new AdminDashboardService(
            _userRepositoryMock.Object,
            _serviceRequestRepositoryMock.Object,
            _proposalRepositoryMock.Object,
            _chatMessageRepositoryMock.Object);
    }

    [Fact]
    public async Task GetDashboardAsync_ShouldAggregateTopLevelMetrics()
    {
        var now = DateTime.UtcNow;

        _userRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>
        {
            new() { Role = UserRole.Admin, IsActive = true },
            new() { Role = UserRole.Provider, IsActive = true },
            new() { Role = UserRole.Client, IsActive = false }
        });

        _serviceRequestRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ServiceRequest>
        {
            new() { Id = Guid.NewGuid(), Status = ServiceRequestStatus.Created, Description = "Pedido A", CreatedAt = now.AddDays(-1), Category = ServiceCategory.Electrical },
            new() { Id = Guid.NewGuid(), Status = ServiceRequestStatus.Completed, Description = "Pedido B", CreatedAt = now.AddDays(-2), Category = ServiceCategory.Plumbing }
        });

        _proposalRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Proposal>
        {
            new() { Id = Guid.NewGuid(), RequestId = Guid.NewGuid(), CreatedAt = now.AddHours(-3), Accepted = true },
            new() { Id = Guid.NewGuid(), RequestId = Guid.NewGuid(), CreatedAt = now.AddHours(-4), Accepted = false }
        });

        _chatMessageRepositoryMock
            .Setup(r => r.GetByPeriodAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new List<ChatMessage>
            {
                new() { Id = Guid.NewGuid(), RequestId = Guid.NewGuid(), ProviderId = Guid.NewGuid(), CreatedAt = now.AddHours(-1), Text = "Ola" }
            });

        var query = new AdminDashboardQueryDto(null, null, "all", null, 1, 20);
        var result = await _service.GetDashboardAsync(query);

        Assert.Equal(3, result.TotalUsers);
        Assert.Equal(2, result.ActiveUsers);
        Assert.Equal(1, result.InactiveUsers);
        Assert.Equal(1, result.TotalAdmins);
        Assert.Equal(1, result.TotalProviders);
        Assert.Equal(1, result.TotalClients);
        Assert.Equal(2, result.TotalRequests);
        Assert.Equal(1, result.ActiveRequests);
        Assert.Equal(2, result.ProposalsInPeriod);
        Assert.Equal(1, result.AcceptedProposalsInPeriod);
        Assert.True(result.ActiveChatConversationsLast24h >= 1);
    }

    [Fact]
    public async Task GetDashboardAsync_ShouldFilterByEventType_AndPaginate()
    {
        var now = DateTime.UtcNow;
        var requestId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        _userRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());

        _serviceRequestRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ServiceRequest>
        {
            new() { Id = requestId, Status = ServiceRequestStatus.Created, Description = "Conserto de fogao", CreatedAt = now.AddHours(-2), Category = ServiceCategory.Appliances }
        });

        _proposalRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Proposal>
        {
            new() { Id = Guid.NewGuid(), RequestId = requestId, CreatedAt = now.AddHours(-1), Accepted = false }
        });

        _chatMessageRepositoryMock
            .Setup(r => r.GetByPeriodAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new List<ChatMessage>
            {
                new() { Id = Guid.NewGuid(), RequestId = requestId, ProviderId = providerId, CreatedAt = now.AddMinutes(-50), Text = "Tenho disponibilidade hoje." }
            });

        var query = new AdminDashboardQueryDto(now.AddDays(-1), now, "request", "fogao", 1, 1);
        var result = await _service.GetDashboardAsync(query);

        Assert.Equal(1, result.TotalEvents);
        Assert.Single(result.RecentEvents);
        Assert.Equal("request", result.RecentEvents[0].Type);
        Assert.Contains("fogao", result.RecentEvents[0].Title, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.PageSize);
    }
}
