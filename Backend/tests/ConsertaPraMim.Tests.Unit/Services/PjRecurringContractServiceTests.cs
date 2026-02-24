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
        repositoryMock.Verify(x => x.UpdateAsync(It.IsAny<PjRecurringContract>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
