using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class PjRecurringContractServiceTests
{
    /// <summary>
    /// Cenario: cliente PJ contrata pacote recorrente com cadencia mensal.
    /// Passos: usuario autenticado valido, payload com SLA/janela operacional e inicio do contrato.
    /// Resultado esperado: contrato ativo criado com proxima renovacao em +1 mes.
    /// </summary>
    [Fact(DisplayName = "PJ recurring contract servico | Criar contrato | Deve criar contrato ativo com renovacao mensal")]
    public async Task CreateAsync_ShouldCreateContract_WhenClientIsPj()
    {
        var clientId = Guid.NewGuid();
        var startsAtUtc = new DateTime(2026, 2, 24, 12, 0, 0, DateTimeKind.Utc);
        PjRecurringContract? persistedContract = null;

        var repositoryMock = new Mock<IPjRecurringContractRepository>();
        repositoryMock
            .Setup(x => x.AddAsync(It.IsAny<PjRecurringContract>(), It.IsAny<CancellationToken>()))
            .Callback<PjRecurringContract, CancellationToken>((contract, _) => persistedContract = contract)
            .Returns(Task.CompletedTask);

        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock
            .Setup(x => x.GetByIdAsync(clientId))
            .ReturnsAsync(new User
            {
                Id = clientId,
                Role = UserRole.Client,
                IsActive = true,
                ClientProfileType = ClientProfileType.Pj,
                ClientPjType = ClientPjType.Empresa
            });
        userRepositoryMock
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(new List<User>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Role = UserRole.Provider,
                    IsActive = true,
                    ProviderProfile = new ProviderProfile
                    {
                        ClientPreference = ProviderClientPreference.Both,
                        Categories = new List<ServiceCategory> { ServiceCategory.Electrical }
                    }
                }
            });

        var service = new PjRecurringContractService(repositoryMock.Object, userRepositoryMock.Object);

        var result = await service.CreateAsync(
            clientId,
            new CreatePjRecurringContractRequestDto(
                ClientPjType.Empresa,
                ServiceCategory.Electrical,
                ProviderClientPreference.Both,
                "Pacote manutencao eletrica",
                "Visitas preventivas mensais para quadro de energia.",
                PjRecurringCadence.Monthly,
                499.90m,
                2,
                12,
                480,
                1080,
                62,
                startsAtUtc,
                true));

        Assert.NotNull(persistedContract);
        Assert.Equal(PjRecurringContractStatus.Active, persistedContract!.Status);
        Assert.Equal(startsAtUtc.AddMonths(1), persistedContract.NextRenewalAtUtc);
        Assert.Equal(ClientPjType.Empresa, persistedContract.ClientPjType);
        Assert.Equal(499.90m, persistedContract.MonthlyAmount);

        Assert.Equal(PjRecurringContractStatus.Active, result.Status);
        Assert.Equal(startsAtUtc.AddMonths(1), result.NextRenewalAtUtc);
        Assert.Equal(1, result.EligibleProvidersCount);
        repositoryMock.Verify(x => x.AddAsync(It.IsAny<PjRecurringContract>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Cenario: cliente PF tenta contratar pacote PJ recorrente.
    /// Passos: usuario autenticado com perfil PF chama fluxo de contratacao.
    /// Resultado esperado: operacao bloqueada por regra de negocio.
    /// </summary>
    [Fact(DisplayName = "PJ recurring contract servico | Criar contrato | Deve bloquear cliente PF")]
    public async Task CreateAsync_ShouldThrowInvalidOperation_WhenClientIsPf()
    {
        var clientId = Guid.NewGuid();

        var repositoryMock = new Mock<IPjRecurringContractRepository>();
        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock
            .Setup(x => x.GetByIdAsync(clientId))
            .ReturnsAsync(new User
            {
                Id = clientId,
                Role = UserRole.Client,
                IsActive = true,
                ClientProfileType = ClientProfileType.Pf
            });

        var service = new PjRecurringContractService(repositoryMock.Object, userRepositoryMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            clientId,
            new CreatePjRecurringContractRequestDto(
                ClientPjType.Outros,
                ServiceCategory.Cleaning,
                ProviderClientPreference.PjOnly,
                "Pacote limpeza recorrente",
                null,
                PjRecurringCadence.Monthly,
                250m,
                1,
                24,
                480,
                1020,
                62,
                DateTime.UtcNow,
                true)));
    }

    /// <summary>
    /// Cenario: cliente PJ tenta contratar pacote em categoria sem prestadores elegiveis.
    /// Passos: base de prestadores contem apenas perfil PF-only para a categoria.
    /// Resultado esperado: contratacao e bloqueada por falta de oferta elegivel.
    /// </summary>
    [Fact(DisplayName = "PJ recurring contract servico | Criar contrato | Deve bloquear quando nao ha prestador elegivel")]
    public async Task CreateAsync_ShouldThrowInvalidOperation_WhenNoEligibleProviderExists()
    {
        var clientId = Guid.NewGuid();
        var repositoryMock = new Mock<IPjRecurringContractRepository>();
        var userRepositoryMock = new Mock<IUserRepository>();

        userRepositoryMock
            .Setup(x => x.GetByIdAsync(clientId))
            .ReturnsAsync(new User
            {
                Id = clientId,
                Role = UserRole.Client,
                IsActive = true,
                ClientProfileType = ClientProfileType.Pj,
                ClientPjType = ClientPjType.Empresa
            });

        userRepositoryMock
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(new List<User>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Role = UserRole.Provider,
                    IsActive = true,
                    ProviderProfile = new ProviderProfile
                    {
                        ClientPreference = ProviderClientPreference.PfOnly,
                        Categories = new List<ServiceCategory> { ServiceCategory.Electrical }
                    }
                }
            });

        var service = new PjRecurringContractService(repositoryMock.Object, userRepositoryMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            clientId,
            new CreatePjRecurringContractRequestDto(
                ClientPjType.Empresa,
                ServiceCategory.Electrical,
                ProviderClientPreference.Both,
                "Pacote eletrico corporativo",
                null,
                PjRecurringCadence.Monthly,
                350m,
                1,
                12,
                480,
                1080,
                62,
                DateTime.UtcNow,
                true)));
    }

    /// <summary>
    /// Cenario: cliente renova contrato que ja atingiu limite de vigencia.
    /// Passos: renovacao calculada ultrapassa EndsAtUtc.
    /// Resultado esperado: contrato finalizado como completed e auto-renew desativado.
    /// </summary>
    [Fact(DisplayName = "PJ recurring contract servico | Renovar contrato | Deve finalizar quando ultrapassa data final")]
    public async Task RenewAsync_ShouldMarkCompleted_WhenRenewalExceedsContractEndDate()
    {
        var clientId = Guid.NewGuid();
        var contractId = Guid.NewGuid();
        PjRecurringContract? updatedContract = null;

        var repositoryMock = new Mock<IPjRecurringContractRepository>();
        repositoryMock
            .Setup(x => x.GetByIdAsync(contractId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PjRecurringContract
            {
                Id = contractId,
                ClientUserId = clientId,
                ClientPjType = ClientPjType.Condominio,
                Category = ServiceCategory.Plumbing,
                ProviderEligibility = ProviderClientPreference.PjOnly,
                Title = "Pacote hidraulica condominio",
                Cadence = PjRecurringCadence.Monthly,
                Status = PjRecurringContractStatus.Active,
                MonthlyAmount = 890m,
                IncludedVisitsPerCycle = 3,
                ResponseSlaHours = 8,
                OperationalWindowStartMinute = 420,
                OperationalWindowEndMinute = 1140,
                OperationalDaysMask = 62,
                StartsAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                NextRenewalAtUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                EndsAtUtc = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc),
                AutoRenew = true
            });

        repositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<PjRecurringContract>(), It.IsAny<CancellationToken>()))
            .Callback<PjRecurringContract, CancellationToken>((contract, _) => updatedContract = contract)
            .Returns(Task.CompletedTask);

        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock
            .Setup(x => x.GetByIdAsync(clientId))
            .ReturnsAsync(new User
            {
                Id = clientId,
                Role = UserRole.Client,
                IsActive = true,
                ClientProfileType = ClientProfileType.Pj,
                ClientPjType = ClientPjType.Condominio
            });
        userRepositoryMock
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(new List<User>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Role = UserRole.Provider,
                    IsActive = true,
                    ProviderProfile = new ProviderProfile
                    {
                        ClientPreference = ProviderClientPreference.PjOnly,
                        Categories = new List<ServiceCategory> { ServiceCategory.Plumbing }
                    }
                }
            });

        var service = new PjRecurringContractService(repositoryMock.Object, userRepositoryMock.Object);
        var renewedAtUtc = new DateTime(2026, 2, 20, 8, 0, 0, DateTimeKind.Utc);

        var result = await service.RenewAsync(
            clientId,
            contractId,
            new RenewPjRecurringContractRequestDto(renewedAtUtc, "renovacao manual"));

        Assert.NotNull(updatedContract);
        Assert.Equal(PjRecurringContractStatus.Completed, updatedContract!.Status);
        Assert.False(updatedContract.AutoRenew);
        Assert.Equal(updatedContract.EndsAtUtc, updatedContract.NextRenewalAtUtc);
        Assert.Equal(renewedAtUtc, updatedContract.LastRenewedAtUtc);
        Assert.Equal(renewedAtUtc, updatedContract.LastPaymentAtUtc);

        Assert.Equal(PjRecurringContractStatus.Completed, result.Status);
        Assert.Equal(1, result.EligibleProvidersCount);
        repositoryMock.Verify(x => x.UpdateAsync(It.IsAny<PjRecurringContract>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Cenario: operacao admin consulta carteira PJ recorrente para visao comercial.
    /// Passos: base com contratos de status/categoria distintos e prestadores elegiveis por preferencia.
    /// Resultado esperado: dashboard retorna totais, breakdown e lista de contratos com contagem de elegiveis.
    /// </summary>
    [Fact(DisplayName = "PJ recurring contract servico | Carteira admin | Deve consolidar KPIs e lista da carteira PJ")]
    public async Task GetAdminPortfolioAsync_ShouldAggregatePortfolioSnapshot()
    {
        var clientA = Guid.NewGuid();
        var clientB = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.Date.AddDays(-2);

        var contracts = new List<PjRecurringContract>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ClientUserId = clientA,
                ClientPjType = ClientPjType.Empresa,
                Category = ServiceCategory.Electrical,
                ProviderEligibility = ProviderClientPreference.Both,
                Title = "Pacote A",
                Cadence = PjRecurringCadence.Monthly,
                Status = PjRecurringContractStatus.Active,
                MonthlyAmount = 500m,
                IncludedVisitsPerCycle = 2,
                ResponseSlaHours = 12,
                OperationalWindowStartMinute = 480,
                OperationalWindowEndMinute = 1080,
                OperationalDaysMask = 62,
                StartsAtUtc = createdAt,
                NextRenewalAtUtc = createdAt.AddMonths(1),
                CreatedAt = createdAt
            },
            new()
            {
                Id = Guid.NewGuid(),
                ClientUserId = clientB,
                ClientPjType = ClientPjType.Condominio,
                Category = ServiceCategory.Plumbing,
                ProviderEligibility = ProviderClientPreference.PjOnly,
                Title = "Pacote B",
                Cadence = PjRecurringCadence.Monthly,
                Status = PjRecurringContractStatus.Delinquent,
                MonthlyAmount = 700m,
                IncludedVisitsPerCycle = 3,
                ResponseSlaHours = 8,
                OperationalWindowStartMinute = 420,
                OperationalWindowEndMinute = 1140,
                OperationalDaysMask = 62,
                StartsAtUtc = createdAt.AddDays(-1),
                NextRenewalAtUtc = createdAt.AddMonths(1).AddDays(-1),
                CreatedAt = createdAt.AddDays(-1)
            }
        };

        var repositoryMock = new Mock<IPjRecurringContractRepository>();
        repositoryMock
            .Setup(x => x.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(contracts);

        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock
            .Setup(x => x.GetAllAsync())
            .ReturnsAsync(new List<User>
            {
                new() { Id = clientA, Role = UserRole.Client, Name = "Cliente A", IsActive = true, ClientProfileType = ClientProfileType.Pj },
                new() { Id = clientB, Role = UserRole.Client, Name = "Cliente B", IsActive = true, ClientProfileType = ClientProfileType.Pj },
                new()
                {
                    Id = Guid.NewGuid(),
                    Role = UserRole.Provider,
                    IsActive = true,
                    ProviderProfile = new ProviderProfile
                    {
                        ClientPreference = ProviderClientPreference.Both,
                        Categories = new List<ServiceCategory> { ServiceCategory.Electrical, ServiceCategory.Plumbing }
                    }
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Role = UserRole.Provider,
                    IsActive = true,
                    ProviderProfile = new ProviderProfile
                    {
                        ClientPreference = ProviderClientPreference.PjOnly,
                        Categories = new List<ServiceCategory> { ServiceCategory.Plumbing }
                    }
                }
            });

        var service = new PjRecurringContractService(repositoryMock.Object, userRepositoryMock.Object);
        var result = await service.GetAdminPortfolioAsync(createdAt.AddDays(-10), DateTime.UtcNow, null);

        Assert.Equal(2, result.TotalContracts);
        Assert.Equal(1, result.ActiveContracts);
        Assert.Equal(1, result.DelinquentContracts);
        Assert.Equal(1200m, result.MonthlyRecurringRevenue);
        Assert.Equal(600m, result.AverageTicket);
        Assert.Equal(2, result.Contracts.Count);
        Assert.Contains(result.Contracts, contract => contract.ClientName == "Cliente A" && contract.EligibleProvidersCount == 1);
        Assert.Contains(result.Contracts, contract => contract.ClientName == "Cliente B" && contract.EligibleProvidersCount == 1);
    }

    /// <summary>
    /// Cenario: operacao admin consulta KPI de receita PJ por janela de renovacao.
    /// Passos: contratos ativos/inadimplentes com renovacoes previstas em dias diferentes.
    /// Resultado esperado: consolidado de MRR e serie diaria com renovacoes/receita prevista.
    /// </summary>
    [Fact(DisplayName = "PJ recurring contract servico | KPI receita | Deve consolidar MRR e serie de renovacao por dia")]
    public async Task GetRevenueKpiAsync_ShouldAggregateRecurringRevenueSeries()
    {
        var fromUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 2, 3, 0, 0, 0, DateTimeKind.Utc);

        var contracts = new List<PjRecurringContract>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ClientUserId = Guid.NewGuid(),
                ClientPjType = ClientPjType.Empresa,
                Category = ServiceCategory.Electrical,
                ProviderEligibility = ProviderClientPreference.Both,
                Title = "Pacote A",
                Cadence = PjRecurringCadence.Monthly,
                Status = PjRecurringContractStatus.Active,
                MonthlyAmount = 500m,
                IncludedVisitsPerCycle = 2,
                ResponseSlaHours = 12,
                OperationalWindowStartMinute = 480,
                OperationalWindowEndMinute = 1080,
                OperationalDaysMask = 62,
                StartsAtUtc = fromUtc.AddDays(-10),
                NextRenewalAtUtc = fromUtc,
                CreatedAt = fromUtc.AddDays(-15)
            },
            new()
            {
                Id = Guid.NewGuid(),
                ClientUserId = Guid.NewGuid(),
                ClientPjType = ClientPjType.Condominio,
                Category = ServiceCategory.Plumbing,
                ProviderEligibility = ProviderClientPreference.PjOnly,
                Title = "Pacote B",
                Cadence = PjRecurringCadence.Monthly,
                Status = PjRecurringContractStatus.Delinquent,
                MonthlyAmount = 700m,
                IncludedVisitsPerCycle = 3,
                ResponseSlaHours = 8,
                OperationalWindowStartMinute = 420,
                OperationalWindowEndMinute = 1140,
                OperationalDaysMask = 62,
                StartsAtUtc = fromUtc.AddDays(-20),
                NextRenewalAtUtc = fromUtc.AddDays(2),
                CreatedAt = fromUtc.AddDays(-20)
            }
        };

        var repositoryMock = new Mock<IPjRecurringContractRepository>();
        repositoryMock
            .Setup(x => x.ListAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(contracts);

        var userRepositoryMock = new Mock<IUserRepository>();
        var service = new PjRecurringContractService(repositoryMock.Object, userRepositoryMock.Object);

        var result = await service.GetRevenueKpiAsync(fromUtc, toUtc);

        Assert.Equal(2, result.ActiveContracts + result.DelinquentContracts);
        Assert.Equal(1200m, result.MonthlyRecurringRevenue);
        Assert.Equal(2, result.RenewalDueContracts);
        Assert.Equal(1200m, result.RenewalDueRevenue);
        Assert.Equal(1200m, result.EstimatedRecurringRevenueForWindow);
        Assert.Equal(3, result.Series.Count);
        Assert.Contains(result.Series, point => point.BucketDateUtc == fromUtc.Date && point.RenewalDueContracts == 1 && point.RenewalDueRevenue == 500m);
        Assert.Contains(result.Series, point => point.BucketDateUtc == toUtc.Date && point.RenewalDueContracts == 1 && point.RenewalDueRevenue == 700m);
    }
}
