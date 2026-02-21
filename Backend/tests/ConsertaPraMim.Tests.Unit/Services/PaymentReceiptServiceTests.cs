using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class PaymentReceiptServiceTests
{
    private readonly Mock<IServiceRequestRepository> _serviceRequestRepositoryMock = new();
    private readonly Mock<IServicePaymentTransactionRepository> _paymentTransactionRepositoryMock = new();
    private readonly PaymentReceiptService _service;

    public PaymentReceiptServiceTests()
    {
        _service = new PaymentReceiptService(
            _serviceRequestRepositoryMock.Object,
            _paymentTransactionRepositoryMock.Object);
    }

    /// <summary>
    /// Cenario: prestador sem vínculo com a ordem tenta consultar recibos da requisicao.
    /// Passos: request pertence a outro ator e o servico avalia permissao antes de acessar transacoes.
    /// Resultado esperado: retorno vazio e nenhuma consulta ao repositório de pagamentos.
    /// </summary>
    [Fact(DisplayName = "Payment receipt servico | Obter por servico requisicao | Deve retornar vazio quando actor tem no access")]
    public async Task GetByServiceRequestAsync_ShouldReturnEmpty_WhenActorHasNoAccess()
    {
        var requestId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var actorProviderId = Guid.NewGuid();

        _serviceRequestRepositoryMock.Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(new ServiceRequest
            {
                Id = requestId,
                ClientId = clientId
            });

        var result = await _service.GetByServiceRequestAsync(
            actorProviderId,
            UserRole.Provider.ToString(),
            requestId);

        Assert.Empty(result);
        _paymentTransactionRepositoryMock.Verify(r => r.GetByServiceRequestIdAsync(It.IsAny<Guid>(), It.IsAny<PaymentTransactionStatus?>()), Times.Never);
    }

    /// <summary>
    /// Cenario: cliente dono da ordem solicita historico de recibos da propria requisicao.
    /// Passos: repositorio entrega transacoes em ordem arbitraria e o servico aplica ordenacao temporal decrescente.
    /// Resultado esperado: lista retornada ao cliente vem ordenada da transacao mais recente para a mais antiga.
    /// </summary>
    [Fact(DisplayName = "Payment receipt servico | Obter por servico requisicao | Deve retornar ordered receipts for cliente")]
    public async Task GetByServiceRequestAsync_ShouldReturnOrderedReceipts_ForClient()
    {
        var requestId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        _serviceRequestRepositoryMock.Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(new ServiceRequest
            {
                Id = requestId,
                ClientId = clientId
            });

        var older = new ServicePaymentTransaction
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            ClientId = clientId,
            ProviderId = providerId,
            Method = PaymentTransactionMethod.Card,
            Status = PaymentTransactionStatus.Pending,
            Amount = 100m,
            Currency = "BRL",
            ProviderTransactionId = "tx-1",
            CheckoutReference = "chk-1",
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        };

        var newer = new ServicePaymentTransaction
        {
            Id = Guid.NewGuid(),
            ServiceRequestId = requestId,
            ClientId = clientId,
            ProviderId = providerId,
            Method = PaymentTransactionMethod.Pix,
            Status = PaymentTransactionStatus.Paid,
            Amount = 120m,
            Currency = "BRL",
            ProviderTransactionId = "tx-2",
            CheckoutReference = "chk-2",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            ProcessedAtUtc = DateTime.UtcNow.AddMinutes(-30)
        };

        _paymentTransactionRepositoryMock.Setup(r => r.GetByServiceRequestIdAsync(requestId, null))
            .ReturnsAsync(new List<ServicePaymentTransaction> { older, newer });

        var result = await _service.GetByServiceRequestAsync(
            clientId,
            UserRole.Client.ToString(),
            requestId);

        Assert.Equal(2, result.Count);
        Assert.Equal(newer.Id, result[0].TransactionId);
        Assert.Equal(older.Id, result[1].TransactionId);
    }

    /// <summary>
    /// Cenario: prestador sem relacao contratual com a ordem consulta recibo por transaction id.
    /// Passos: servico encontra a ordem, valida acesso e detecta ausencia de vinculo por provider/proposta aceita.
    /// Resultado esperado: falha com errorCode forbidden, impedindo exposição de dados financeiros.
    /// </summary>
    [Fact(DisplayName = "Payment receipt servico | Obter por transaction | Deve retornar proibido quando prestador nao linked")]
    public async Task GetByTransactionAsync_ShouldReturnForbidden_WhenProviderIsNotLinked()
    {
        var requestId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var actorProviderId = Guid.NewGuid();

        _serviceRequestRepositoryMock.Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(new ServiceRequest
            {
                Id = requestId,
                ClientId = Guid.NewGuid()
            });

        var result = await _service.GetByTransactionAsync(
            actorProviderId,
            UserRole.Provider.ToString(),
            requestId,
            transactionId);

        Assert.False(result.Success);
        Assert.Equal("forbidden", result.ErrorCode);
    }

    /// <summary>
    /// Cenario: prestador vinculado por proposta aceita consulta recibo especifico da transacao.
    /// Passos: ordem inclui proposta aceita do ator e repositorio retorna transacao correspondente.
    /// Resultado esperado: operacao bem-sucedida com DTO de recibo preenchido para a transaction solicitada.
    /// </summary>
    [Fact(DisplayName = "Payment receipt servico | Obter por transaction | Deve retornar receipt quando prestador linked por proposal")]
    public async Task GetByTransactionAsync_ShouldReturnReceipt_WhenProviderLinkedByProposal()
    {
        var requestId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var actorProviderId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        _serviceRequestRepositoryMock.Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(new ServiceRequest
            {
                Id = requestId,
                ClientId = clientId,
                Proposals =
                [
                    new Proposal
                    {
                        ProviderId = actorProviderId,
                        Accepted = true
                    }
                ]
            });

        _paymentTransactionRepositoryMock.Setup(r => r.GetByIdAsync(transactionId))
            .ReturnsAsync(new ServicePaymentTransaction
            {
                Id = transactionId,
                ServiceRequestId = requestId,
                ClientId = clientId,
                ProviderId = actorProviderId,
                Method = PaymentTransactionMethod.Pix,
                Status = PaymentTransactionStatus.Paid,
                Amount = 50m,
                Currency = "BRL",
                ProviderTransactionId = "tx-provider",
                CheckoutReference = "checkout-ref",
                CreatedAt = DateTime.UtcNow
            });

        var result = await _service.GetByTransactionAsync(
            actorProviderId,
            UserRole.Provider.ToString(),
            requestId,
            transactionId);

        Assert.True(result.Success);
        Assert.NotNull(result.Receipt);
        Assert.Equal(transactionId, result.Receipt!.TransactionId);
    }
}
