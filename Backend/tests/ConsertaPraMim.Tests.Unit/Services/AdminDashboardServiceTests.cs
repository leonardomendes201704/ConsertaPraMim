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
    private readonly Mock<IZipGeocodingService> _zipGeocodingServiceMock;
    private readonly Mock<ILandingLeadRepository> _landingLeadRepositoryMock;
    private readonly Mock<ILandingAccessEventRepository> _landingAccessEventRepositoryMock;
    private readonly AdminDashboardService _service;

    public AdminDashboardServiceTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _serviceRequestRepositoryMock = new Mock<IServiceRequestRepository>();
        _proposalRepositoryMock = new Mock<IProposalRepository>();
        _chatMessageRepositoryMock = new Mock<IChatMessageRepository>();
        _userPresenceTrackerMock = new Mock<IUserPresenceTracker>();
        _planGovernanceServiceMock = new Mock<IPlanGovernanceService>();
        _zipGeocodingServiceMock = new Mock<IZipGeocodingService>();
        _landingLeadRepositoryMock = new Mock<ILandingLeadRepository>();
        _landingAccessEventRepositoryMock = new Mock<ILandingAccessEventRepository>();

        _planGovernanceServiceMock
            .Setup(s => s.GetProviderPlanOffersAsync(It.IsAny<DateTime?>()))
            .ReturnsAsync(new List<ProviderPlanOfferDto>
            {
                new(ProviderPlan.Bronze, "Bronze", 79.9m, 0m, 79.9m, null),
                new(ProviderPlan.Silver, "Silver", 129.9m, 0m, 129.9m, null),
                new(ProviderPlan.Gold, "Gold", 199.9m, 0m, 199.9m, null)
            });
        _landingLeadRepositoryMock
            .Setup(repository => repository.GetByPeriodAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LandingLead>());
        _landingAccessEventRepositoryMock
            .Setup(repository => repository.GetByPeriodAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LandingAccessEvent>());

        _service = new AdminDashboardService(
            _userRepositoryMock.Object,
            _serviceRequestRepositoryMock.Object,
            _proposalRepositoryMock.Object,
            _chatMessageRepositoryMock.Object,
            _userPresenceTrackerMock.Object,
            _planGovernanceServiceMock.Object,
            _zipGeocodingServiceMock.Object,
            landingLeadRepository: _landingLeadRepositoryMock.Object,
            landingAccessEventRepository: _landingAccessEventRepositoryMock.Object);
    }

    /// <summary>
    /// Cenario: dashboard admin consolida indicadores macro de usuarios, demandas, propostas, receita e chat.
    /// Passos: popula repositorios com amostra mista (admin/prestador/cliente, pedidos e propostas) e executa GetDashboardAsync.
    /// Resultado esperado: metricas de topo refletem os volumes corretos e receita considera apenas provedores pagantes.
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
    /// Cenario: dashboard admin precisa consolidar topo do funil da landing.
    /// Passos: injeta visitas com visitorId estavel e leads cliente/prestador no mesmo periodo.
    /// Resultado esperado: dashboard calcula visitas, visitantes unicos, cadastros por origem e taxa de conversao.
    /// </summary>
    [Fact(DisplayName = "Admin dashboard servico | Obter dashboard | Deve calcular KPIs da landing e taxa de conversao")]
    public async Task GetDashboardAsync_ShouldAggregateLandingKpisAndConversion()
    {
        var now = DateTime.UtcNow;

        _userRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());
        _serviceRequestRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ServiceRequest>());
        _proposalRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Proposal>());
        _chatMessageRepositoryMock
            .Setup(r => r.GetByPeriodAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new List<ChatMessage>());
        _userPresenceTrackerMock
            .Setup(t => t.CountOnlineUsers(It.IsAny<IEnumerable<Guid>>()))
            .Returns(0);
        _landingAccessEventRepositoryMock
            .Setup(repository => repository.GetByPeriodAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LandingAccessEvent>
            {
                new() { VisitorId = "visitor-a", Path = "/", CreatedAt = now.AddHours(-3) },
                new() { VisitorId = "visitor-a", Path = "/Cliente", CreatedAt = now.AddHours(-2) },
                new() { VisitorId = "visitor-b", Path = "/Prestador", CreatedAt = now.AddHours(-1) }
            });
        _landingLeadRepositoryMock
            .Setup(repository => repository.GetByPeriodAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LandingLead>
            {
                new() { Origin = LandingLeadOrigin.Client, VisitorId = "visitor-a", FullName = "Cliente 1", Phone = "13999999999", Email = "cliente@teste.com", City = "Praia Grande", State = "SP", Neighborhood = "Ocian", CreatedAt = now.AddMinutes(-50) },
                new() { Origin = LandingLeadOrigin.Provider, VisitorId = "visitor-b", FullName = "Prestador 1", Phone = "13999999998", Email = "prestador@teste.com", City = "Santos", State = "SP", Neighborhood = "Centro", CreatedAt = now.AddMinutes(-20) }
            });

        var result = await _service.GetDashboardAsync(
            new AdminDashboardQueryDto(now.AddDays(-1), now, "all", null, null, 1, 20));

        Assert.Equal(3, result.LandingVisitsInPeriod);
        Assert.Equal(2, result.LandingUniqueVisitorsInPeriod);
        Assert.Equal(1, result.LandingClientSignupsInPeriod);
        Assert.Equal(1, result.LandingProviderSignupsInPeriod);
        Assert.Equal(2, result.LandingConvertedVisitorsInPeriod);
        Assert.Equal(66.7m, result.LandingConversionRatePercent);
    }

    /// <summary>
    /// Cenario: operador filtra feed de eventos recentes por tipo e termo de busca com paginacao.
    /// Passos: monta dados de request/proposal/chat e consulta dashboard com eventType=request, search=fogao e pageSize=1.
    /// Resultado esperado: apenas evento aderente ao filtro retorna, com metadados de pagina corretos.
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
        Assert.Equal("client_request_opened", result.RecentEvents[0].Type);
        Assert.Contains("fogao", result.RecentEvents[0].Description, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.PageSize);
    }

    /// <summary>
    /// Cenario: dashboard precisa destacar falhas de pagamento por prestador e por canal de cobranca.
    /// Passos: injeta transacoes pagas/falhas em PIX e cartao para diferentes provedores e executa agregacao.
    /// Resultado esperado: ranking por prestador e contadores por canal exibem somente eventos com status Failed.
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
    /// Cenario: distribucao de pedidos por categoria deve priorizar volume e desempatar por nome.
    /// Passos: cria requests em categorias com contagens diferentes (incluindo nome vindo de CategoryDefinition).
    /// Resultado esperado: ordenacao final fica por contagem decrescente e, em empate, por nome crescente.
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
    /// Cenario: painel de qualidade precisa ranquear notas e sinalizar perfis com avaliacao critica.
    /// Passos: injeta reviews de prestadores e clientes com medias distintas e executa consolidacao.
    /// Resultado esperado: rankings mostram maiores medias no topo e outliers incluem perfis de baixa avaliacao.
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
    /// Cenario: dashboard deve expor sinais de retencao (recompra) e qualidade pos-servico (NPS + score composto).
    /// Passos: cria cliente com atendimento concluido/pago e nova abertura de pedido apos conclusao; registra reviews de cliente com NPS/composite.
    /// Resultado esperado: taxa de recompra, base convertida e indicadores de qualidade retornam com os valores consolidados no recorte.
    /// </summary>
    [Fact(DisplayName = "Admin dashboard servico | Obter dashboard | Deve calcular indicadores de recompra e NPS operacional")]
    public async Task GetDashboardAsync_ShouldCalculateRepurchaseAndOperationalNpsIndicators()
    {
        var now = DateTime.UtcNow;
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var completedRequestId = Guid.NewGuid();

        _userRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>
        {
            new() { Id = clientId, Name = "Cliente Retencao", Role = UserRole.Client, IsActive = true },
            new() { Id = providerId, Name = "Prestador Retencao", Role = UserRole.Provider, IsActive = true }
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
                Id = completedRequestId,
                ClientId = clientId,
                Status = ServiceRequestStatus.Completed,
                Description = "Atendimento concluido",
                CreatedAt = now.AddDays(-20),
                Category = ServiceCategory.Electrical,
                PaymentTransactions = new List<ServicePaymentTransaction>
                {
                    new()
                    {
                        ServiceRequestId = completedRequestId,
                        ProviderId = providerId,
                        Method = PaymentTransactionMethod.Pix,
                        Status = PaymentTransactionStatus.Paid,
                        Currency = "BRL",
                        Amount = 180m,
                        CreatedAt = now.AddDays(-16)
                    }
                },
                Appointments = new List<ServiceAppointment>
                {
                    new()
                    {
                        Status = ServiceAppointmentStatus.Completed,
                        CreatedAt = now.AddDays(-17),
                        CompletedAtUtc = now.AddDays(-15)
                    }
                },
                Reviews = new List<Review>
                {
                    new()
                    {
                        RequestId = completedRequestId,
                        ClientId = clientId,
                        ProviderId = providerId,
                        ReviewerUserId = clientId,
                        ReviewerRole = UserRole.Client,
                        RevieweeUserId = providerId,
                        RevieweeRole = UserRole.Provider,
                        Rating = 5,
                        NpsScore = 10,
                        CompositeScore = 90m,
                        CreatedAt = now.AddDays(-14)
                    },
                    new()
                    {
                        RequestId = completedRequestId,
                        ClientId = clientId,
                        ProviderId = providerId,
                        ReviewerUserId = clientId,
                        ReviewerRole = UserRole.Client,
                        RevieweeUserId = providerId,
                        RevieweeRole = UserRole.Provider,
                        Rating = 3,
                        NpsScore = 4,
                        CompositeScore = 60m,
                        CreatedAt = now.AddDays(-13)
                    }
                }
            },
            new()
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                Status = ServiceRequestStatus.Created,
                Description = "Nova demanda apos conclusao",
                CreatedAt = now.AddDays(-10),
                Category = ServiceCategory.Plumbing
            }
        });

        var result = await _service.GetDashboardAsync(
            new AdminDashboardQueryDto(now.AddDays(-30), now, "all", null, null, 1, 20));

        Assert.Equal(1, result.RepurchaseEligibleClients);
        Assert.Equal(1, result.RepurchaseConvertedClients);
        Assert.Equal(100.0m, result.RepurchaseRatePercent);

        Assert.Equal(2, result.OperationalNpsRespondents);
        Assert.Equal(0.0m, result.OperationalNpsScore);
        Assert.Equal(75.0m, result.OperationalQualityScore);
        Assert.Equal(2, result.ReviewedServicesInPeriod);
    }

    /// <summary>
    /// Cenario: receita de assinatura deve considerar somente planos pagos e ignorar Trial.
    /// Passos: prepara quatro prestadores (Bronze/Silver/Gold/Trial) e solicita calculo de receita mensal.
    /// Resultado esperado: total e breakdown por plano incluem apenas Bronze, Silver e Gold.
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
    /// Cenario: operador filtra pedidos por status operacional (ex.: OnSite) no dashboard.
    /// Passos: cria requests com appointments em estados diferentes e aplica o filtro operacional na consulta.
    /// Resultado esperado: somente requests aderentes ao filtro entram nas contagens e listas retornadas.
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
    /// Cenario: dashboard operacional calcula KPIs de agenda (SLA, remarcacao, cancelamento) e de lembretes.
    /// Passos: semeia appointments com diferentes desfechos e configura contagem de envios/falhas de reminder.
    /// Resultado esperado: percentuais e totais refletem exatamente a composicao da amostra operacional.
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
        var zipGeocodingServiceMock = new Mock<IZipGeocodingService>();

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
            zipGeocodingServiceMock.Object,
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

    /// <summary>
    /// Cenario: snapshot do mapa operacional precisa expor bairro por pedido para consolidacao de cobertura.
    /// Passos: cria pedido georreferenciado com bairro preenchido e consulta GetCoverageMapAsync.
    /// Resultado esperado: o payload retornado inclui o bairro no item de request sem perder os demais campos do mapa.
    /// </summary>
    [Fact(DisplayName = "Admin dashboard servico | Coverage map | Deve incluir bairro no payload dos pedidos")]
    public async Task GetCoverageMapAsync_ShouldIncludeNeighborhoodInRequestPayload()
    {
        var requestId = Guid.NewGuid();

        _userRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<User>());
        _serviceRequestRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<ServiceRequest>
        {
            new()
            {
                Id = requestId,
                Status = ServiceRequestStatus.Created,
                Description = "Pedido com bairro",
                Category = ServiceCategory.Electrical,
                AddressCity = "Praia Grande",
                AddressNeighborhood = "Ocian",
                AddressStreet = "Rua Monteiro Lobato",
                Latitude = -24.0219,
                Longitude = -46.4618,
                CreatedAt = DateTime.UtcNow.AddHours(-1)
            }
        });

        var result = await _service.GetCoverageMapAsync();

        var request = Assert.Single(result.Requests);
        Assert.Equal(requestId, request.RequestId);
        Assert.Equal("Ocian", request.AddressNeighborhood);
        Assert.Equal("Praia Grande", request.AddressCity);
    }
}
