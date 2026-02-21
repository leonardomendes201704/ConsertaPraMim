using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class ProviderCreditServiceTests
{
    private readonly Mock<IProviderCreditRepository> _providerCreditRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IAdminAuditLogRepository> _adminAuditLogRepositoryMock;
    private readonly ProviderCreditService _service;

    public ProviderCreditServiceTests()
    {
        _providerCreditRepositoryMock = new Mock<IProviderCreditRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _adminAuditLogRepositoryMock = new Mock<IAdminAuditLogRepository>();

        _service = new ProviderCreditService(
            _providerCreditRepositoryMock.Object,
            _userRepositoryMock.Object,
            _adminAuditLogRepositoryMock.Object);
    }

    /// <summary>
    /// Cenario: prestador consulta saldo atual da carteira de créditos.
    /// Passos: serviço valida perfil Provider, garante carteira e lê balanço persistido no repositório.
    /// Resultado esperado: DTO de balance retorna providerId correto e valor corrente da carteira.
    /// </summary>
    [Fact(DisplayName = "Prestador credito servico | Obter balance | Deve retornar wallet balance")]
    public async Task GetBalanceAsync_ShouldReturnWalletBalance()
    {
        var providerId = Guid.NewGuid();
        _userRepositoryMock.Setup(x => x.GetByIdAsync(providerId))
            .ReturnsAsync(new User { Id = providerId, Role = UserRole.Provider });
        _providerCreditRepositoryMock.Setup(x => x.EnsureWalletAsync(providerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderCreditWallet
            {
                ProviderId = providerId,
                CurrentBalance = 42.5m,
                LastMovementAtUtc = DateTime.UtcNow.AddMinutes(-5)
            });

        var result = await _service.GetBalanceAsync(providerId);

        Assert.Equal(providerId, result.ProviderId);
        Assert.Equal(42.5m, result.CurrentBalance);
    }

    /// <summary>
    /// Cenario: tentativa de débito acima do saldo disponível na carteira.
    /// Passos: factory de lançamento recebe wallet com R$10 e requisição de débito de R$30.
    /// Resultado esperado: mutação rejeitada com errorCode insufficient_balance e sem auditoria.
    /// </summary>
    [Fact(DisplayName = "Prestador credito servico | Apply mutation | Deve retornar insufficient balance quando debit exceeds balance")]
    public async Task ApplyMutationAsync_ShouldReturnInsufficientBalance_WhenDebitExceedsBalance()
    {
        var providerId = Guid.NewGuid();
        _userRepositoryMock.Setup(x => x.GetByIdAsync(providerId))
            .ReturnsAsync(new User { Id = providerId, Role = UserRole.Provider });

        _providerCreditRepositoryMock
            .Setup(x => x.AppendEntryAsync(
                providerId,
                It.IsAny<Func<ProviderCreditWallet, ProviderCreditLedgerEntry>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Guid, Func<ProviderCreditWallet, ProviderCreditLedgerEntry>, CancellationToken>((id, entryFactory, _) =>
            {
                var wallet = new ProviderCreditWallet
                {
                    Id = Guid.NewGuid(),
                    ProviderId = id,
                    CurrentBalance = 10m
                };

                // Will throw validation from service factory.
                var entry = entryFactory(wallet);
                return Task.FromResult(entry);
            });

        var result = await _service.ApplyMutationAsync(
            new ProviderCreditMutationRequestDto(
                providerId,
                ProviderCreditLedgerEntryType.Debit,
                30m,
                "Consumo mensalidade"),
            actorUserId: Guid.NewGuid(),
            actorEmail: "admin@teste.com");

        Assert.False(result.Success);
        Assert.Equal("insufficient_balance", result.ErrorCode);
        _adminAuditLogRepositoryMock.Verify(x => x.AddAsync(It.IsAny<AdminAuditLog>()), Times.Never);
    }

    /// <summary>
    /// Cenario: concessão de crédito manual aplicada com sucesso no ledger do prestador.
    /// Passos: serviço cria entry de grant, recalcula saldo e registra auditoria com ator administrativo.
    /// Resultado esperado: resultado de sucesso com entry/balance preenchidos e audit log de criação do grant.
    /// </summary>
    [Fact(DisplayName = "Prestador credito servico | Apply mutation | Deve persistir grant e audit")]
    public async Task ApplyMutationAsync_ShouldPersistGrantAndAudit()
    {
        var providerId = Guid.NewGuid();
        _userRepositoryMock.Setup(x => x.GetByIdAsync(providerId))
            .ReturnsAsync(new User { Id = providerId, Role = UserRole.Provider });

        _providerCreditRepositoryMock
            .Setup(x => x.AppendEntryAsync(
                providerId,
                It.IsAny<Func<ProviderCreditWallet, ProviderCreditLedgerEntry>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Guid, Func<ProviderCreditWallet, ProviderCreditLedgerEntry>, CancellationToken>((id, entryFactory, _) =>
            {
                var wallet = new ProviderCreditWallet
                {
                    Id = Guid.NewGuid(),
                    ProviderId = id,
                    CurrentBalance = 0m
                };

                var entry = entryFactory(wallet);
                entry.Id = Guid.NewGuid();
                entry.CreatedAt = DateTime.UtcNow;
                return Task.FromResult(entry);
            });

        var actorUserId = Guid.NewGuid();
        var result = await _service.ApplyMutationAsync(
            new ProviderCreditMutationRequestDto(
                providerId,
                ProviderCreditLedgerEntryType.Grant,
                25m,
                "Premio por qualidade",
                Source: "AdminPortal",
                ReferenceType: "ManualGrant"),
            actorUserId,
            "admin@teste.com");

        Assert.True(result.Success);
        Assert.NotNull(result.Balance);
        Assert.Equal(25m, result.Balance!.CurrentBalance);
        Assert.NotNull(result.Entry);
        Assert.Equal(ProviderCreditLedgerEntryType.Grant, result.Entry!.EntryType);

        _adminAuditLogRepositoryMock.Verify(x => x.AddAsync(It.Is<AdminAuditLog>(log =>
            log.ActorUserId == actorUserId &&
            log.Action == "ProviderCreditGrantCreated")), Times.Once);
    }
}
