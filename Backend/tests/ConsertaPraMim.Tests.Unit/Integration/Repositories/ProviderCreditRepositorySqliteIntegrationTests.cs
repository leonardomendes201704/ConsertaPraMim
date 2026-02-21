using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;

namespace ConsertaPraMim.Tests.Unit.Integration.Repositories;

public class ProviderCreditRepositorySqliteIntegrationTests
{
    [Fact(DisplayName = "Prestador credito repository sqlite integracao | Ensure wallet | Deve criar single wallet per prestador")]
    public async Task EnsureWalletAsync_ShouldCreateSingleWalletPerProvider()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        await using var dbContext = context;
        using var sqliteConnection = connection;
        var repository = new ProviderCreditRepository(dbContext);

        var providerId = Guid.NewGuid();
        await dbContext.Users.AddAsync(new User
        {
            Id = providerId,
            Name = "Prestador Teste",
            Email = "prestador.credito@teste.com",
            PasswordHash = "hash",
            Phone = "21900000000",
            Role = UserRole.Provider
        });
        await dbContext.SaveChangesAsync();

        var first = await repository.EnsureWalletAsync(providerId);
        var second = await repository.EnsureWalletAsync(providerId);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(providerId, second.ProviderId);
        Assert.Equal(1, dbContext.ProviderCreditWallets.Count());
    }

    [Fact(DisplayName = "Prestador credito repository sqlite integracao | Append entry | Deve persistir ledger e atualizar balance")]
    public async Task AppendEntryAsync_ShouldPersistLedgerAndUpdateBalance()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        await using var dbContext = context;
        using var sqliteConnection = connection;
        var repository = new ProviderCreditRepository(dbContext);

        var providerId = Guid.NewGuid();
        await dbContext.Users.AddAsync(new User
        {
            Id = providerId,
            Name = "Prestador Ledger",
            Email = "prestador.ledger@teste.com",
            PasswordHash = "hash",
            Phone = "21900000001",
            Role = UserRole.Provider
        });
        await dbContext.SaveChangesAsync();

        await repository.AppendEntryAsync(providerId, wallet => new ProviderCreditLedgerEntry
        {
            EntryType = ProviderCreditLedgerEntryType.Grant,
            Amount = 30m,
            BalanceBefore = wallet.CurrentBalance,
            BalanceAfter = wallet.CurrentBalance + 30m,
            Reason = "Premio",
            Source = "Teste",
            EffectiveAtUtc = DateTime.UtcNow
        });

        await repository.AppendEntryAsync(providerId, wallet => new ProviderCreditLedgerEntry
        {
            EntryType = ProviderCreditLedgerEntryType.Debit,
            Amount = 12m,
            BalanceBefore = wallet.CurrentBalance,
            BalanceAfter = wallet.CurrentBalance - 12m,
            Reason = "Consumo mensalidade",
            Source = "Teste",
            EffectiveAtUtc = DateTime.UtcNow.AddMinutes(1)
        });

        var walletSnapshot = await repository.GetWalletAsync(providerId);
        var (statementItems, totalCount) = await repository.GetStatementAsync(
            providerId,
            null,
            null,
            null,
            1,
            20);
        var (debitItems, debitCount) = await repository.GetStatementAsync(
            providerId,
            null,
            null,
            ProviderCreditLedgerEntryType.Debit,
            1,
            20);

        Assert.NotNull(walletSnapshot);
        Assert.Equal(18m, walletSnapshot!.CurrentBalance);
        Assert.Equal(2, totalCount);
        Assert.Equal(2, statementItems.Count);
        Assert.Single(debitItems);
        Assert.Equal(1, debitCount);
        Assert.Equal(ProviderCreditLedgerEntryType.Debit, debitItems[0].EntryType);
        Assert.Equal(18m, debitItems[0].BalanceAfter);
    }
}
