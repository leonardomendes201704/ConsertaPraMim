using System.Security.Claims;
using ConsertaPraMim.API.Controllers;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;

namespace ConsertaPraMim.Tests.Unit.Controllers;

public class PaymentsControllerTests
{
    [Fact(DisplayName = "Payments controller | Simulacao mock deve usar segredo da API e retornar sucesso")]
    public async Task SimulateMockWebhook_ShouldUseApiSecret_WhenClientCallsSimulation()
    {
        var actorUserId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var providerId = Guid.NewGuid();
        const string providerTransactionId = "mock_txn_test_001";

        var checkoutServiceMock = new Mock<IPaymentCheckoutService>();
        var webhookServiceMock = new Mock<IPaymentWebhookService>();
        var receiptServiceMock = new Mock<IPaymentReceiptService>();

        PaymentWebhookRequestDto? capturedWebhookRequest = null;

        receiptServiceMock
            .Setup(service => service.GetByTransactionAsync(
                actorUserId,
                UserRole.Client.ToString(),
                requestId,
                transactionId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentReceiptResultDto(
                true,
                new PaymentReceiptDto(
                    transactionId,
                    requestId,
                    actorUserId,
                    "Cliente Teste",
                    providerId,
                    "Prestador Teste",
                    150m,
                    "BRL",
                    "Pix",
                    "Pending",
                    DateTime.UtcNow,
                    null,
                    null,
                    null,
                    providerTransactionId,
                    "mock_checkout_ref",
                    "CPM-TEST-001",
                    null)));

        webhookServiceMock
            .Setup(service => service.ProcessWebhookAsync(
                It.IsAny<PaymentWebhookRequestDto>(),
                It.IsAny<CancellationToken>()))
            .Callback<PaymentWebhookRequestDto, CancellationToken>((request, _) => capturedWebhookRequest = request)
            .ReturnsAsync(new PaymentWebhookProcessResultDto(
                true,
                TransactionId: transactionId,
                ProviderTransactionId: providerTransactionId,
                Status: PaymentTransactionStatus.Paid));

        var controller = BuildController(
            checkoutServiceMock.Object,
            webhookServiceMock.Object,
            receiptServiceMock.Object,
            actorUserId,
            UserRole.Client);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payments:Mock:WebhookSecret"] = "api-secret"
            })
            .Build();

        var result = await controller.SimulateMockWebhook(
            new SimulateMockPaymentRequestDto(requestId, transactionId, "paid"),
            configuration,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        Assert.NotNull(capturedWebhookRequest);
        Assert.Equal(PaymentTransactionProvider.Mock, capturedWebhookRequest!.Provider);
        Assert.Equal("api-secret", capturedWebhookRequest.Signature);
        Assert.Contains(providerTransactionId, capturedWebhookRequest.RawBody, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Payments controller | Simulacao mock deve rejeitar status invalido")]
    public async Task SimulateMockWebhook_ShouldReturnBadRequest_WhenStatusIsInvalid()
    {
        var checkoutServiceMock = new Mock<IPaymentCheckoutService>();
        var webhookServiceMock = new Mock<IPaymentWebhookService>();
        var receiptServiceMock = new Mock<IPaymentReceiptService>();

        var controller = BuildController(
            checkoutServiceMock.Object,
            webhookServiceMock.Object,
            receiptServiceMock.Object,
            Guid.NewGuid(),
            UserRole.Client);

        var configuration = new ConfigurationBuilder().Build();

        var result = await controller.SimulateMockWebhook(
            new SimulateMockPaymentRequestDto(Guid.NewGuid(), Guid.NewGuid(), "processing"),
            configuration,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        webhookServiceMock.Verify(
            service => service.ProcessWebhookAsync(It.IsAny<PaymentWebhookRequestDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static PaymentsController BuildController(
        IPaymentCheckoutService checkoutService,
        IPaymentWebhookService webhookService,
        IPaymentReceiptService receiptService,
        Guid actorUserId,
        UserRole actorRole)
    {
        var controller = new PaymentsController(checkoutService, webhookService, receiptService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, actorUserId.ToString()),
                    new Claim(ClaimTypes.Role, actorRole.ToString())
                },
                "TestAuth"));

        return controller;
    }
}
