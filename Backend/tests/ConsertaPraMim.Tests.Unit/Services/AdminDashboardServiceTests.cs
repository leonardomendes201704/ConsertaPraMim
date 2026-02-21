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

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin dashboard servico | Obter dashboard | Deve aggregate top level metrics.
    /// </summary>
    [Fact(DisplayName = "Admin dashboard servico | Obter dashboard | Deve aggregate top level metrics")]
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

        var query = new AdminDashboardQueryDto(null, null, "all", null, null, 1, 20);
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

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin dashboard servico | Obter dashboard | Deve filter por event type e paginate.
    /// </summary>
    [Fact(DisplayName = "Admin dashboard servico | Obter dashboard | Deve filter por event type e paginate")]
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

        var query = new AdminDashboardQueryDto(now.AddDays(-1), now, "request", null, "fogao", 1, 1);
        var result = await _service.GetDashboardAsync(query);

        Assert.Equal(1, result.TotalEvents);
        Assert.Single(result.RecentEvents);
        Assert.Equal("request", result.RecentEvents[0].Type);
        Assert.Contains("fogao", result.RecentEvents[0].Title, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.PageSize);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin dashboard servico | Obter dashboard | Deve aggregate payment failures por prestador e channel.
    /// </summary>
    [Fact(DisplayName = "Admin dashboard servico | Obter dashboard | Deve aggregate payment failures por prestador e channel")]
    public async Task GetDashboardAsync_ShouldAggregatePaymentFailures_ByProviderAndChannel()
    {
        var now = DateTime.UtcNow;
        var providerAId = Guid.NewGuid();
        var providerBId = Guid.NewGuid();
        var requestAId = Guid.NewGuid();
        var requestBId = Guid.NewGuid();

        _userRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>
        {
            new() { Id = providerAId, Name = "Prestador Alpha", Role = UserRole.Provider, IsActive = true },
            new() { Id = providerBId, Name = "Prestador Beta", Role = UserRole.Provider, IsActive = true }
        });

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
                Id = requestAId,
                Status = ServiceRequestStatus.Completed,
                Description = "Pedido A",
                CreatedAt = now.AddHours(-4),
                Category = ServiceCategory.Electrical,
                PaymentTransactions = new List<ServicePaymentTransaction>
                {
                    new()
                    {
                        ServiceRequestId = requestAId,
                        ProviderId = providerAId,
                        Method = PaymentTransactionMethod.Pix,
                        Status = PaymentTransactionStatus.Failed,
                        Currency = "BRL",
                        Amount = 150m,
                        CreatedAt = now.AddHours(-3)
                    },
                    new()
                    {
                        ServiceRequestId = requestAId,
                        ProviderId = providerAId,
                        Method = PaymentTransactionMethod.Pix,
                        Status = PaymentTransactionStatus.Paid,
                        Currency = "BRL",
                        Amount = 150m,
                        CreatedAt = now.AddHours(-2)
                    }
                }
            },
            new()
            {
                Id = requestBId,
                Status = ServiceRequestStatus.Completed,
                Description = "Pedido B",
                CreatedAt = now.AddHours(-2),
                Category = ServiceCategory.Plumbing,
                PaymentTransactions = new List<ServicePaymentTransaction>
                {
                    new()
                    {
                        ServiceRequestId = requestBId,
                        ProviderId = providerAId,
                        Method = PaymentTransactionMethod.Card,
                        Status = PaymentTransactionStatus.Failed,
                        Currency = "BRL",
                        Amount = 250m,
                        CreatedAt = now.AddHours(-1),
                        UpdatedAt = now.AddMinutes(-30)
                    },
                    new()
                    {
                        ServiceRequestId = requestBId,
                        ProviderId = providerBId,
                        Method = PaymentTransactionMethod.Card,
                        Status = PaymentTransactionStatus.Failed,
                        Currency = "BRL",
                        Amount = 300m,
                        CreatedAt = now.AddMinutes(-50),
                        ProcessedAtUtc = now.AddMinutes(-25)
                    }
                }
            }
        });

        var result = await _service.GetDashboardAsync(
            new AdminDashboardQueryDto(now.AddDays(-1), now, "all", null, null, 1, 20));

        Assert.NotNull(result.PaymentFailuresByProvider);
        Assert.NotNull(result.PaymentFailuresByChannel);

        Assert.Equal(2, result.PaymentFailuresByProvider!.Count);
        Assert.Equal(providerAId, result.PaymentFailuresByProvider[0].ProviderId);
        Assert.Equal("Prestador Alpha", result.PaymentFailuresByProvider[0].ProviderName);
        Assert.Equal(2, result.PaymentFailuresByProvider[0].FailedTransactions);
        Assert.Equal(2, result.PaymentFailuresByProvider[0].AffectedRequests);

        var channelCounts = result.PaymentFailuresByChannel!.ToDictionary(x => x.Status, x => x.Count);
        Assert.Equal(1, channelCounts["PIX"]);
        Assert.Equal(2, channelCounts["Cartao"]);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin dashboard servico | Obter dashboard | Deve pedido requisicoes por category count desc then name asc.
    /// </summary>
    [Fact(DisplayName = "Admin dashboard servico | Obter dashboard | Deve pedido requisicoes por category count desc then name asc")]
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
            new AdminDashboardQueryDto(now.AddDays(-1), now, "all", null, null, 1, 20));

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

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin dashboard servico | Obter dashboard | Deve build review ranking e outliers.
    /// </summary>
    [Fact(DisplayName = "Admin dashboard servico | Obter dashboard | Deve build review ranking e outliers")]
    public async Task GetDashboardAsync_ShouldBuildReviewRankingAndOutliers()
    {
        var now = DateTime.UtcNow;
        var providerAId = Guid.NewGuid();
        var providerBId = Guid.NewGuid();
        var clientAId = Guid.NewGuid();
        var clientBId = Guid.NewGuid();

        _userRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>
        {
            new() { Id = providerAId, Name = "Prestador Alpha", Role = UserRole.Provider, IsActive = true },
            new() { Id = providerBId, Name = "Prestador Beta", Role = UserRole.Provider, IsActive = true },
            new() { Id = clientAId, Name = "Cliente Alpha", Role = UserRole.Client, IsActive = true },
            new() { Id = clientBId, Name = "Cliente Beta", Role = UserRole.Client, IsActive = true }
        });

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
                Status = ServiceRequestStatus.Completed,
                Description = "Pedido 1",
                CreatedAt = now.AddDays(-2),
                Category = ServiceCategory.Electrical,
                Reviews = new List<Review>
                {
                    new() { RevieweeUserId = providerAId, RevieweeRole = UserRole.Provider, Rating = 1, CreatedAt = now.AddDays(-2) },
                    new() { RevieweeUserId = providerAId, RevieweeRole = UserRole.Provider, Rating = 1, CreatedAt = now.AddDays(-1) },
                    new() { RevieweeUserId = providerAId, RevieweeRole = UserRole.Provider, Rating = 2, CreatedAt = now.AddHours(-20) },
                    new() { RevieweeUserId = providerBId, RevieweeRole = UserRole.Provider, Rating = 5, CreatedAt = now.AddDays(-2) },
                    new() { RevieweeUserId = providerBId, RevieweeRole = UserRole.Provider, Rating = 5, CreatedAt = now.AddDays(-1) },
                    new() { RevieweeUserId = providerBId, RevieweeRole = UserRole.Provider, Rating = 4, CreatedAt = now.AddHours(-10) },
                    new() { RevieweeUserId = clientAId, RevieweeRole = UserRole.Client, Rating = 5, CreatedAt = now.AddDays(-1) },
                    new() { RevieweeUserId = clientAId, RevieweeRole = UserRole.Client, Rating = 4, CreatedAt = now.AddHours(-9) },
                    new() { RevieweeUserId = clientBId, RevieweeRole = UserRole.Client, Rating = 1, CreatedAt = now.AddHours(-8) },
                    new() { RevieweeUserId = clientBId, RevieweeRole = UserRole.Client, Rating = 1, CreatedAt = now.AddHours(-7) },
                    new() { RevieweeUserId = clientBId, RevieweeRole = UserRole.Client, Rating = 2, CreatedAt = now.AddHours(-6) }
                }
            }
        });

        var result = await _service.GetDashboardAsync(new AdminDashboardQueryDto(now.AddDays(-30), now, "all", null, null, 1, 20));

        Assert.NotNull(result.ProviderReviewRanking);
        Assert.NotNull(result.ClientReviewRanking);
        Assert.NotNull(result.ReviewOutliers);

        Assert.True(result.ProviderReviewRanking!.Count >= 2);
        Assert.Equal("Prestador Beta", result.ProviderReviewRanking[0].UserName);
        Assert.True(result.ProviderReviewRanking[0].AverageRating > result.ProviderReviewRanking[1].AverageRating);

        Assert.True(result.ClientReviewRanking!.Count >= 2);
        Assert.Equal("Cliente Alpha", result.ClientReviewRanking[0].UserName);

        Assert.Contains(result.ReviewOutliers!, item => item.UserName == "Prestador Alpha" && item.UserRole == "Prestador");
        Assert.Contains(result.ReviewOutliers!, item => item.UserName == "Cliente Beta" && item.UserRole == "Cliente");
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin dashboard servico | Obter dashboard | Deve calculate subscription revenue excluding trial.
    /// </summary>
    [Fact(DisplayName = "Admin dashboard servico | Obter dashboard | Deve calculate subscription revenue excluding trial")]
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
        var result = await _service.GetDashboardAsync(new AdminDashboardQueryDto(null, null, "all", null, null, 1, 20));

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

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin dashboard servico | Obter dashboard | Deve filter requisicoes por operational status quando filter provided.
    /// </summary>
    [Fact(DisplayName = "Admin dashboard servico | Obter dashboard | Deve filter requisicoes por operational status quando filter provided")]
    public async Task GetDashboardAsync_ShouldFilterRequestsByOperationalStatus_WhenFilterIsProvided()
    {
        var now = DateTime.UtcNow;
        var requestOnSiteId = Guid.NewGuid();
        var requestInServiceId = Guid.NewGuid();

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
                Id = requestOnSiteId,
                Status = ServiceRequestStatus.Scheduled,
                Description = "Pedido no local",
                CreatedAt = now.AddHours(-1),
                Category = ServiceCategory.Electrical,
                Appointments =
                {
                    new ServiceAppointment
                    {
                        Status = ServiceAppointmentStatus.Arrived,
                        ArrivedAtUtc = now.AddMinutes(-20)
                    }
                }
            },
            new()
            {
                Id = requestInServiceId,
                Status = ServiceRequestStatus.InProgress,
                Description = "Pedido em atendimento",
                CreatedAt = now.AddHours(-2),
                Category = ServiceCategory.Plumbing,
                Appointments =
                {
                    new ServiceAppointment
                    {
                        Status = ServiceAppointmentStatus.InProgress,
                        StartedAtUtc = now.AddMinutes(-40)
                    }
                }
            }
        });

        var result = await _service.GetDashboardAsync(
            new AdminDashboardQueryDto(now.AddDays(-1), now, "all", "OnSite", null, 1, 20));

        Assert.Equal(1, result.TotalRequests);
        Assert.Equal(1, result.RequestsInPeriod);
        Assert.Single(result.RequestsByStatus);
        Assert.Equal("Scheduled", result.RequestsByStatus[0].Status);
    }

    /// <summary>
    /// Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: Admin dashboard servico | Obter dashboard | Deve compute agenda operational e reminder kpis.
    /// </summary>
    [Fact(DisplayName = "Admin dashboard servico | Obter dashboard | Deve compute agenda operational e reminder kpis")]
    public async Task GetDashboardAsync_ShouldComputeAgendaOperationalAndReminderKpis()
    {
        var now = DateTime.UtcNow;

        var userRepositoryMock = new Mock<IUserRepository>();
        var serviceRequestRepositoryMock = new Mock<IServiceRequestRepository>();
        var proposalRepositoryMock = new Mock<IProposalRepository>();
        var chatMessageRepositoryMock = new Mock<IChatMessageRepository>();
        var userPresenceTrackerMock = new Mock<IUserPresenceTracker>();
        var planGovernanceServiceMock = new Mock<IPlanGovernanceService>();
        var reminderRepositoryMock = new Mock<IAppointmentReminderDispatchRepository>();

        planGovernanceServiceMock
            .Setup(s => s.GetProviderPlanOffersAsync(It.IsAny<DateTime?>()))
            .ReturnsAsync(Array.Empty<ProviderPlanOfferDto>());
        userRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());
        proposalRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Proposal>());
        chatMessageRepositoryMock
            .Setup(r => r.GetByPeriodAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new List<ChatMessage>());
        userPresenceTrackerMock
            .Setup(t => t.CountOnlineUsers(It.IsAny<IEnumerable<Guid>>()))
            .Returns(0);

        serviceRequestRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ServiceRequest>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Status = ServiceRequestStatus.Scheduled,
                Description = "Agenda KPI",
                CreatedAt = now.AddHours(-3),
                Category = ServiceCategory.Electrical,
                Appointments = new List<ServiceAppointment>
                {
                    new()
                    {
                        Status = ServiceAppointmentStatus.Confirmed,
                        CreatedAt = now.AddHours(-2),
                        ExpiresAtUtc = now.AddHours(2),
                        ConfirmedAtUtc = now.AddHours(1)
                    },
                    new()
                    {
                        Status = ServiceAppointmentStatus.ExpiredWithoutProviderAction,
                        CreatedAt = now.AddHours(-2),
                        ExpiresAtUtc = now.AddHours(-1)
                    },
                    new()
                    {
                        Status = ServiceAppointmentStatus.RescheduleRequestedByClient,
                        CreatedAt = now.AddHours(-2),
                        ExpiresAtUtc = now.AddHours(3),
                        RescheduleRequestedAtUtc = now.AddHours(-1)
                    },
                    new()
                    {
                        Status = ServiceAppointmentStatus.CancelledByClient,
                        CreatedAt = now.AddHours(-2),
                        ExpiresAtUtc = now.AddHours(1),
                        CancelledAtUtc = now.AddMinutes(-30)
                    }
                }
            }
        });

        reminderRepositoryMock
            .Setup(r => r.CountAsync(
                null,
                AppointmentReminderDispatchStatus.Sent,
                null,
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>()))
            .ReturnsAsync(8);
        reminderRepositoryMock
            .Setup(r => r.CountAsync(
                null,
                AppointmentReminderDispatchStatus.FailedRetryable,
                null,
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>()))
            .ReturnsAsync(1);
        reminderRepositoryMock
            .Setup(r => r.CountAsync(
                null,
                AppointmentReminderDispatchStatus.FailedPermanent,
                null,
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>()))
            .ReturnsAsync(1);

        var service = new AdminDashboardService(
            userRepositoryMock.Object,
            serviceRequestRepositoryMock.Object,
            proposalRepositoryMock.Object,
            chatMessageRepositoryMock.Object,
            userPresenceTrackerMock.Object,
            planGovernanceServiceMock.Object,
            reminderRepositoryMock.Object);

        var result = await service.GetDashboardAsync(new AdminDashboardQueryDto(
            now.AddDays(-1),
            now.AddDays(1),
            "all",
            null,
            null,
            1,
            20));

        Assert.Equal(25.0m, result.AppointmentConfirmationInSlaRatePercent);
        Assert.Equal(25.0m, result.AppointmentRescheduleRatePercent);
        Assert.Equal(25.0m, result.AppointmentCancellationRatePercent);
        Assert.Equal(20.0m, result.ReminderFailureRatePercent);
        Assert.Equal(10, result.ReminderAttemptsInPeriod);
        Assert.Equal(2, result.ReminderFailuresInPeriod);
    }
}
