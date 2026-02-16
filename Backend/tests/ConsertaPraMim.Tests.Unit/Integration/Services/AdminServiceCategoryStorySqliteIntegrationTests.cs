using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Data;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Integration.Services;

public class AdminServiceCategoryStorySqliteIntegrationTests
{
    [Fact]
    public async Task CategoryCrud_ShouldPersistChanges_AndWriteAuditTrail()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            context.ServiceCategoryDefinitions.Add(new ServiceCategoryDefinition
            {
                Name = "Hidraulica",
                Slug = "hidraulica",
                LegacyCategory = ServiceCategory.Plumbing,
                IsActive = true
            });
            await context.SaveChangesAsync();

            var service = new AdminServiceCategoryService(
                new ServiceCategoryRepository(context),
                new AdminAuditLogRepository(context));

            var actorUserId = Guid.NewGuid();
            const string actorEmail = "admin.categoria@teste.com";

            var createResult = await service.CreateAsync(
                new AdminCreateServiceCategoryRequestDto("Automacao Residencial", null, "Other"),
                actorUserId,
                actorEmail);

            Assert.True(createResult.Success);
            Assert.NotNull(createResult.Category);
            Assert.Equal("automacao-residencial", createResult.Category!.Slug);
            Assert.True(createResult.Category.IsActive);

            var updateResult = await service.UpdateAsync(
                createResult.Category.Id,
                new AdminUpdateServiceCategoryRequestDto("Automacao Predial", "automacao-predial", "Electrical"),
                actorUserId,
                actorEmail);

            Assert.True(updateResult.Success);
            Assert.NotNull(updateResult.Category);
            Assert.Equal("Automacao Predial", updateResult.Category!.Name);
            Assert.Equal("automacao-predial", updateResult.Category.Slug);
            Assert.Equal("Electrical", updateResult.Category.LegacyCategory);

            var statusResult = await service.UpdateStatusAsync(
                createResult.Category.Id,
                new AdminUpdateServiceCategoryStatusRequestDto(false, "Categoria descontinuada"),
                actorUserId,
                actorEmail);

            Assert.True(statusResult.Success);

            var allCategories = await service.GetAllAsync(includeInactive: true);
            var inactiveCategory = Assert.Single(allCategories, c => c.Id == createResult.Category.Id);
            Assert.False(inactiveCategory.IsActive);

            var onlyActive = await service.GetAllAsync(includeInactive: false);
            Assert.DoesNotContain(onlyActive, c => c.Id == createResult.Category.Id);

            var auditEntries = await context.AdminAuditLogs
                .AsNoTracking()
                .Where(x => x.TargetType == "ServiceCategory" && x.TargetId == createResult.Category.Id)
                .ToListAsync();

            Assert.Equal(3, auditEntries.Count);
            Assert.Contains(auditEntries, x => x.Action == "ServiceCategoryCreated");
            Assert.Contains(auditEntries, x => x.Action == "ServiceCategoryUpdated");
            Assert.Contains(auditEntries, x => x.Action == "ServiceCategoryStatusChanged");
        }
    }

    [Fact]
    public async Task DashboardAggregation_ShouldRankRequestsByCategory_CountDescThenNameAsc()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var now = DateTime.UtcNow;
            var fromUtc = now.AddDays(-1);
            var toUtc = now.AddHours(1);

            var eletrica = new ServiceCategoryDefinition
            {
                Name = "Eletrica",
                Slug = "eletrica",
                LegacyCategory = ServiceCategory.Electrical,
                IsActive = true
            };

            var automacao = new ServiceCategoryDefinition
            {
                Name = "Automacao",
                Slug = "automacao",
                LegacyCategory = ServiceCategory.Other,
                IsActive = true
            };

            var client = CreateUser("Cliente Dashboard", "cliente.dashboard@teste.com", UserRole.Client);

            context.ServiceCategoryDefinitions.AddRange(eletrica, automacao);
            context.Users.Add(client);
            context.ServiceRequests.AddRange(
                CreateRequest(client.Id, ServiceCategory.Electrical, eletrica.Id, "Troca de tomada", now.AddHours(-6)),
                CreateRequest(client.Id, ServiceCategory.Electrical, eletrica.Id, "Instalacao de luminaria", now.AddHours(-5)),
                CreateRequest(client.Id, ServiceCategory.Other, automacao.Id, "Configuracao de automacao", now.AddHours(-4)),
                CreateRequest(client.Id, ServiceCategory.Plumbing, null, "Vazamento na pia", now.AddHours(-3)));
            await context.SaveChangesAsync();

            var planGovernanceMock = new Mock<IPlanGovernanceService>();
            planGovernanceMock
                .Setup(s => s.GetProviderPlanOffersAsync(It.IsAny<DateTime?>()))
                .ReturnsAsync(Array.Empty<ProviderPlanOfferDto>());

            var dashboardService = new AdminDashboardService(
                new UserRepository(context),
                new ServiceRequestRepository(context),
                new ProposalRepository(context),
                new ChatMessageRepository(context),
                new NoOpUserPresenceTracker(),
                planGovernanceMock.Object);

            var result = await dashboardService.GetDashboardAsync(
                new AdminDashboardQueryDto(fromUtc, toUtc, "all", null, null, 1, 20));

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
                    Assert.Equal("Hidraulica", third.Category);
                    Assert.Equal(1, third.Count);
                });
        }
    }

    [Fact]
    public async Task InactivatingCategory_ShouldKeepOpenRequestsOperational_AndBlockNewOnes()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var eletrica = new ServiceCategoryDefinition
            {
                Name = "Eletrica",
                Slug = "eletrica",
                LegacyCategory = ServiceCategory.Electrical,
                IsActive = true
            };

            var hidraulica = new ServiceCategoryDefinition
            {
                Name = "Hidraulica",
                Slug = "hidraulica",
                LegacyCategory = ServiceCategory.Plumbing,
                IsActive = true
            };

            var client = CreateUser("Cliente 01", "cliente.continuidade@teste.com", UserRole.Client);
            var provider = CreateProviderWithProfile(
                "Prestador 01",
                "prestador.continuidade@teste.com",
                categories: new List<ServiceCategory> { ServiceCategory.Electrical },
                radiusKm: 40,
                baseLatitude: -24.00,
                baseLongitude: -46.40);

            context.ServiceCategoryDefinitions.AddRange(eletrica, hidraulica);
            context.Users.AddRange(client, provider);

            var existingRequest = CreateRequest(
                client.Id,
                ServiceCategory.Electrical,
                eletrica.Id,
                "Troca de disjuntor",
                DateTime.UtcNow.AddHours(-2));

            context.ServiceRequests.Add(existingRequest);
            await context.SaveChangesAsync();

            var adminServiceCategoryService = new AdminServiceCategoryService(
                new ServiceCategoryRepository(context),
                new AdminAuditLogRepository(context));

            var inactivation = await adminServiceCategoryService.UpdateStatusAsync(
                eletrica.Id,
                new AdminUpdateServiceCategoryStatusRequestDto(false, "Categoria em revisao"),
                Guid.NewGuid(),
                "admin.regressao@teste.com");

            Assert.True(inactivation.Success);

            var geocodingMock = new Mock<IZipGeocodingService>();
            geocodingMock
                .Setup(s => s.ResolveCoordinatesAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
                .ReturnsAsync(("11704150", -24.001, -46.401, "Rua Nova", "Praia Grande"));

            var notificationMock = new Mock<INotificationService>();
            notificationMock
                .Setup(s => s.SendNotificationAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>()))
                .Returns(Task.CompletedTask);

            var requestService = new ServiceRequestService(
                new ServiceRequestRepository(context),
                new ServiceCategoryRepository(context),
                new UserRepository(context),
                geocodingMock.Object,
                notificationMock.Object);

            var providerVisibleRequests = (await requestService.GetAllAsync(provider.Id, UserRole.Provider.ToString())).ToList();
            Assert.Contains(providerVisibleRequests, r => r.Id == existingRequest.Id);

            var clientView = await requestService.GetByIdAsync(existingRequest.Id, client.Id, UserRole.Client.ToString());
            Assert.NotNull(clientView);

            var createDto = new CreateServiceRequestDto(
                CategoryId: eletrica.Id,
                Category: null,
                Description: "Nova instalacao eletrica",
                Street: "Rua Nova",
                City: "Praia Grande",
                Zip: "11704150",
                Lat: -24.001,
                Lng: -46.401);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => requestService.CreateAsync(client.Id, createDto));
            Assert.Contains("inativa", exception.Message, StringComparison.OrdinalIgnoreCase);

            var requestCount = await context.ServiceRequests.CountAsync();
            Assert.Equal(1, requestCount);
        }
    }

    private static User CreateUser(string name, string email, UserRole role)
    {
        return new User
        {
            Name = name,
            Email = email,
            PasswordHash = "hash",
            Phone = "11999999999",
            Role = role,
            IsActive = true
        };
    }

    private static User CreateProviderWithProfile(
        string name,
        string email,
        List<ServiceCategory> categories,
        double radiusKm,
        double baseLatitude,
        double baseLongitude)
    {
        var provider = CreateUser(name, email, UserRole.Provider);
        provider.ProviderProfile = new ProviderProfile
        {
            UserId = provider.Id,
            RadiusKm = radiusKm,
            BaseLatitude = baseLatitude,
            BaseLongitude = baseLongitude,
            Categories = categories
        };

        return provider;
    }

    private static ServiceRequest CreateRequest(
        Guid clientId,
        ServiceCategory category,
        Guid? categoryDefinitionId,
        string description,
        DateTime createdAtUtc)
    {
        return new ServiceRequest
        {
            ClientId = clientId,
            Category = category,
            CategoryDefinitionId = categoryDefinitionId,
            Status = ServiceRequestStatus.Created,
            Description = description,
            AddressStreet = "Rua Teste",
            AddressCity = "Praia Grande",
            AddressZip = "11704150",
            Latitude = -24.001,
            Longitude = -46.401,
            CreatedAt = createdAtUtc
        };
    }

    private sealed class NoOpUserPresenceTracker : IUserPresenceTracker
    {
        public UserPresenceChangedEvent? RegisterConnection(string connectionId, Guid userId) => null;
        public UserPresenceChangedEvent? UnregisterConnection(string connectionId) => null;
        public bool IsOnline(Guid userId) => false;
        public int CountOnlineUsers(IEnumerable<Guid> userIds) => 0;
    }
}
