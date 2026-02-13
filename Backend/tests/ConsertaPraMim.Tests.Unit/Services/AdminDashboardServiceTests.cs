using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
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
    private readonly Mock<IUserPresenceTracker> _userPresenceTrackerMock;
    private readonly Mock<IPlanGovernanceService> _planGovernanceServiceMock;
    private readonly AdminDashboardService _service;

    public AdminDashboardServiceTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _serviceRequestRepositoryMock = new Mock<IServiceRequestRepository>();
        _proposalRepositoryMock = new Mock<IProposalRepository>();
        _chatMessageRepositoryMock = new Mock<IChatMessageRepository>();
        _userPresenceTrackerMock = new Mock<IUserPresenceTracker>();
        _planGovernanceServiceMock = new Mock<IPlanGovernanceService>();

        _planGovernanceServiceMock
            .Setup(s => s.GetProviderPlanOffersAsync(It.IsAny<DateTime?>()))
            .ReturnsAsync(new List<ProviderPlanOfferDto>
            {
                new(ProviderPlan.Bronze, "Bronze", 79.9m, 0m, 79.9m, null),
                new(ProviderPlan.Silver, "Silver", 129.9m, 0m, 129.9m, null),
                new(ProviderPlan.Gold, "Gold", 199.9m, 0m, 199.9m, null)
            });

        _service = new AdminDashboardService(
            _userRepositoryMock.Object,
            _serviceRequestRepositoryMock.Object,
            _proposalRepositoryMock.Object,
            _chatMessageRepositoryMock.Object,
            _userPresenceTrackerMock.Object,
            _planGovernanceServiceMock.Object);
    }

    [Fact]
    public async Task GetDashboardAsync_ShouldAggregateTopLevelMetrics()
    {
        var now = DateTime.UtcNow;

        _userRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>
        {
            new() { Role = UserRole.Admin, IsActive = true },
            new()
            {
                Role = UserRole.Provider,
                IsActive = true,
                ProviderProfile = new ProviderProfile
                {
                    Plan = ProviderPlan.Bronze,
                    Categories = new List<ServiceCategory> { ServiceCategory.Electrical }
                }
            },
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

        _userPresenceTrackerMock
            .Setup(t => t.CountOnlineUsers(It.IsAny<IEnumerable<Guid>>()))
            .Returns((IEnumerable<Guid> ids) => ids.Count());

        var query = new AdminDashboardQueryDto(null, null, "all", null, 1, 20);
        var result = await _service.GetDashboardAsync(query);

        Assert.Equal(3, result.TotalUsers);
        Assert.Equal(2, result.ActiveUsers);
        Assert.Equal(1, result.InactiveUsers);
        Assert.Equal(1, result.TotalAdmins);
        Assert.Equal(1, result.TotalProviders);
        Assert.Equal(1, result.TotalClients);
        Assert.Equal(1, result.OnlineProviders);
        Assert.Equal(1, result.OnlineClients);
        Assert.Equal(1, result.PayingProviders);
        Assert.Equal(79.90m, result.MonthlySubscriptionRevenue);
        Assert.Single(result.RevenueByPlan);
        Assert.Equal("Bronze", result.RevenueByPlan[0].Plan);
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

    [Fact]
    public async Task GetDashboardAsync_ShouldOrderRequestsByCategory_CountDescThenNameAsc()
    {
        var now = DateTime.UtcNow;

        _userRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());
        _proposalRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Proposal>());
        _chatMessageRepositoryMock
            .Setup(r => r.GetByPeriodAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new List<ChatMessage>());
        _userPresenceTrackerMock
            .Setup(t => t.CountOnlineUsers(It.IsAny<IEnumerable<Guid>>()))
            .Returns(0);

        _serviceRequestRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ServiceRequest>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Status = ServiceRequestStatus.Created,
                Description = "Pedido 1",
                CreatedAt = now.AddHours(-2),
                Category = ServiceCategory.Electrical,
                CategoryDefinition = new ServiceCategoryDefinition { Name = "Eletrica", Slug = "eletrica", LegacyCategory = ServiceCategory.Electrical, IsActive = true }
            },
            new()
            {
                Id = Guid.NewGuid(),
                Status = ServiceRequestStatus.Created,
                Description = "Pedido 2",
                CreatedAt = now.AddHours(-3),
                Category = ServiceCategory.Electrical,
                CategoryDefinition = new ServiceCategoryDefinition { Name = "Eletrica", Slug = "eletrica", LegacyCategory = ServiceCategory.Electrical, IsActive = true }
            },
            new()
            {
                Id = Guid.NewGuid(),
                Status = ServiceRequestStatus.Created,
                Description = "Pedido 3",
                CreatedAt = now.AddHours(-4),
                Category = ServiceCategory.Plumbing
            },
            new()
            {
                Id = Guid.NewGuid(),
                Status = ServiceRequestStatus.Created,
                Description = "Pedido 4",
                CreatedAt = now.AddHours(-5),
                Category = ServiceCategory.Other,
                CategoryDefinition = new ServiceCategoryDefinition { Name = "Automacao", Slug = "automacao", LegacyCategory = ServiceCategory.Other, IsActive = true }
            }
        });

        var result = await _service.GetDashboardAsync(
            new AdminDashboardQueryDto(now.AddDays(-1), now, "all", null, 1, 20));

        Assert.Equal(3, result.RequestsByCategory.Count);
        Assert.Collection(result.RequestsByCategory,
            first =>
            {
                Assert.Equal("Eletrica", first.Category);
                Assert.Equal(2, first.Count);
            },
            second =>
            {
                Assert.Equal("Automacao", second.Category);
                Assert.Equal(1, second.Count);
            },
            third =>
            {
                Assert.Equal(ServiceCategory.Plumbing.ToPtBr(), third.Category);
                Assert.Equal(1, third.Count);
            });
    }

    [Fact]
    public async Task GetDashboardAsync_ShouldCalculateSubscriptionRevenue_ExcludingTrial()
    {
        // Arrange
        _userRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>
        {
            new()
            {
                Role = UserRole.Provider,
                IsActive = true,
                ProviderProfile = new ProviderProfile { Plan = ProviderPlan.Bronze, Categories = new List<ServiceCategory>() }
            },
            new()
            {
                Role = UserRole.Provider,
                IsActive = true,
                ProviderProfile = new ProviderProfile { Plan = ProviderPlan.Silver, Categories = new List<ServiceCategory>() }
            },
            new()
            {
                Role = UserRole.Provider,
                IsActive = true,
                ProviderProfile = new ProviderProfile { Plan = ProviderPlan.Gold, Categories = new List<ServiceCategory>() }
            },
            new()
            {
                Role = UserRole.Provider,
                IsActive = true,
                ProviderProfile = new ProviderProfile { Plan = ProviderPlan.Trial, Categories = new List<ServiceCategory>() }
            }
        });

        _serviceRequestRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ServiceRequest>());
        _proposalRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Proposal>());
        _chatMessageRepositoryMock
            .Setup(r => r.GetByPeriodAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new List<ChatMessage>());
        _userPresenceTrackerMock
            .Setup(t => t.CountOnlineUsers(It.IsAny<IEnumerable<Guid>>()))
            .Returns(0);

        // Act
        var result = await _service.GetDashboardAsync(new AdminDashboardQueryDto(null, null, "all", null, 1, 20));

        // Assert
        Assert.Equal(3, result.PayingProviders);
        Assert.Equal(409.70m, result.MonthlySubscriptionRevenue);
        Assert.Collection(result.RevenueByPlan,
            first =>
            {
                Assert.Equal("Gold", first.Plan);
                Assert.Equal(1, first.Providers);
                Assert.Equal(199.90m, first.TotalMonthlyRevenue);
            },
            second =>
            {
                Assert.Equal("Silver", second.Plan);
                Assert.Equal(1, second.Providers);
                Assert.Equal(129.90m, second.TotalMonthlyRevenue);
            },
            third =>
            {
                Assert.Equal("Bronze", third.Plan);
                Assert.Equal(1, third.Providers);
                Assert.Equal(79.90m, third.TotalMonthlyRevenue);
            });
    }
}
