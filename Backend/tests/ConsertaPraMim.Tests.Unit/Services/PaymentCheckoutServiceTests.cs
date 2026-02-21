using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Services;

public class PaymentCheckoutServiceTests
{
    private readonly Mock<IServiceRequestRepository> _serviceRequestRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IServicePaymentTransactionRepository> _paymentTransactionRepositoryMock = new();
    private readonly Mock<IPaymentService> _paymentServiceMock = new();
    private readonly PaymentCheckoutService _service;

    public PaymentCheckoutServiceTests()
    {
        _service = new PaymentCheckoutService(
            _serviceRequestRepositoryMock.Object,
            _userRepositoryMock.Object,
            _paymentTransactionRepositoryMock.Object,
            _paymentServiceMock.Object);
    }

    /// <summary>
    /// Cenario: cliente tenta abrir novo checkout para servico que ja possui transacao paga do mesmo prestador.
    /// Passos: prepara request concluida com proposta aceita e historico contendo pagamento com status Paid.
    /// Resultado esperado: servico bloqueia nova sessao com erro already_paid e nao aciona gateway de pagamento.
    /// </summary>
    [Fact(DisplayName = "Payment checkout servico | Criar checkout | Deve retornar already paid quando prestador already tem paid transaction")]
    public async Task CreateCheckoutAsync_ShouldReturnAlreadyPaid_WhenProviderAlreadyHasPaidTransaction()
    {
        var requestId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();

        _serviceRequestRepositoryMock.Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(new ServiceRequest
            {
                Id = requestId,
                ClientId = clientId,
                Status = ServiceRequestStatus.Completed,
                Proposals =
                [
                    new Proposal
                    {
                        ProviderId = providerId,
                        Accepted = true,
                        EstimatedValue = 120m
                    }
                ]
            });
        _userRepositoryMock.Setup(r => r.GetByIdAsync(providerId))
            .ReturnsAsync(new User
            {
                Id = providerId,
                Role = UserRole.Provider,
                IsActive = true
            });
        _paymentTransactionRepositoryMock
            .Setup(r => r.GetByServiceRequestIdAsync(requestId, null))
            .ReturnsAsync(new List<ServicePaymentTransaction>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ServiceRequestId = requestId,
                    ProviderId = providerId,
                    Status = PaymentTransactionStatus.Paid,
                    Amount = 120m
                }
            });

        var result = await _service.CreateCheckoutAsync(
            clientId,
            UserRole.Client.ToString(),
            new CreatePaymentCheckoutRequestDto(requestId, "pix", providerId));

        Assert.False(result.Success);
        Assert.Equal("already_paid", result.ErrorCode);
        _paymentServiceMock.Verify(s => s.CreateCheckoutSessionAsync(It.IsAny<PaymentCheckoutRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Cenario: ultima tentativa de cobranca falhou e cliente precisa conseguir reprocessar o checkout.
    /// Passos: injeta transacao anterior com status Failed e configura provider de pagamento para retornar nova sessao.
    /// Resultado esperado: fluxo permite retry, cria nova sessao em cartao e chama gateway exatamente uma vez.
    /// </summary>
    [Fact(DisplayName = "Payment checkout servico | Criar checkout | Deve allow retry quando last transaction falha")]
    public async Task CreateCheckoutAsync_ShouldAllowRetry_WhenLastTransactionFailed()
    {
        var requestId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        var session = new PaymentCheckoutSessionDto(
            PaymentTransactionProvider.Mock,
            "mock_ref",
            "https://checkout.local/mock_ref",
            "mock_txn",
            PaymentTransactionStatus.Pending,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(30));

        _serviceRequestRepositoryMock.Setup(r => r.GetByIdAsync(requestId))
            .ReturnsAsync(new ServiceRequest
            {
                Id = requestId,
                ClientId = clientId,
                Status = ServiceRequestStatus.Completed,
                Proposals =
                [
                    new Proposal
                    {
                        ProviderId = providerId,
                        Accepted = true,
                        EstimatedValue = 99m
                    }
                ]
            });
        _userRepositoryMock.Setup(r => r.GetByIdAsync(providerId))
            .ReturnsAsync(new User
            {
                Id = providerId,
                Role = UserRole.Provider,
                IsActive = true
            });
        _paymentTransactionRepositoryMock
            .Setup(r => r.GetByServiceRequestIdAsync(requestId, null))
            .ReturnsAsync(new List<ServicePaymentTransaction>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ServiceRequestId = requestId,
                    ProviderId = providerId,
                    Status = PaymentTransactionStatus.Failed,
                    Amount = 99m
                }
            });
        _paymentServiceMock
            .Setup(s => s.CreateCheckoutSessionAsync(It.IsAny<PaymentCheckoutRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _paymentTransactionRepositoryMock
            .Setup(r => r.AddOrGetByProviderTransactionIdAsync(It.IsAny<ServicePaymentTransaction>()))
            .ReturnsAsync((ServicePaymentTransaction transaction) =>
            {
                transaction.Id = Guid.NewGuid();
                return (transaction, true);
            });

        var result = await _service.CreateCheckoutAsync(
            clientId,
            UserRole.Client.ToString(),
            new CreatePaymentCheckoutRequestDto(requestId, "card", providerId));

        Assert.True(result.Success);
        Assert.Equal(PaymentTransactionMethod.Card, result.Method);
        _paymentServiceMock.Verify(s => s.CreateCheckoutSessionAsync(It.IsAny<PaymentCheckoutRequestDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
