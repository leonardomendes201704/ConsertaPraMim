using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Services;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Infrastructure.Data;
using ConsertaPraMim.Infrastructure.Repositories;
using ConsertaPraMim.Infrastructure.Services;
using ConsertaPraMim.Tests.Unit.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConsertaPraMim.Tests.Unit.Integration.Services;

public class PaymentWebhookServiceSqliteIntegrationTests
{
    [Fact(DisplayName = "Payment webhook servico sqlite integracao | Process webhook | Deve atualizar transaction para paid quando signature e payload valido")]
    public async Task ProcessWebhookAsync_ShouldUpdateTransactionToPaid_WhenSignatureAndPayloadAreValid()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var seeded = await SeedTransactionAsync(context, PaymentTransactionStatus.Pending);
            var service = BuildService(context);

            var webhookRequest = BuildWebhookRequest(
                seeded.ProviderTransactionId,
                status: "paid",
                eventId: "evt_paid_001",
                amount: 132.45m,
                currency: "brl");

            var result = await service.ProcessWebhookAsync(webhookRequest);

            Assert.True(result.Success);
            Assert.Equal(PaymentTransactionStatus.Paid, result.Status);
            Assert.Equal(seeded.TransactionId, result.TransactionId);

            var stored = await context.ServicePaymentTransactions
                .AsNoTracking()
                .SingleAsync(t => t.Id == seeded.TransactionId);

            Assert.Equal(PaymentTransactionStatus.Paid, stored.Status);
            Assert.Equal("evt_paid_001", stored.ProviderEventId);
            Assert.Equal(132.45m, stored.Amount);
            Assert.Equal("BRL", stored.Currency);
            Assert.NotNull(stored.ProcessedAtUtc);
            Assert.Null(stored.RefundedAtUtc);
        }
    }

    [Fact(DisplayName = "Payment webhook servico sqlite integracao | Process webhook | Deve idempotent quando event replayed")]
    public async Task ProcessWebhookAsync_ShouldBeIdempotent_WhenEventIsReplayed()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var seeded = await SeedTransactionAsync(context, PaymentTransactionStatus.Pending);
            var service = BuildService(context);

            var webhookRequest = BuildWebhookRequest(
                seeded.ProviderTransactionId,
                status: "failed",
                eventId: "evt_failed_001",
                amount: 89.90m,
                failureCode: "card_declined",
                failureReason: "Cartao recusado");

            var firstResult = await service.ProcessWebhookAsync(webhookRequest);
            var firstSnapshot = await context.ServicePaymentTransactions
                .AsNoTracking()
                .SingleAsync(t => t.Id == seeded.TransactionId);

            Assert.True(firstResult.Success);
            Assert.Equal(PaymentTransactionStatus.Failed, firstResult.Status);
            Assert.NotNull(firstSnapshot.UpdatedAt);

            var secondResult = await service.ProcessWebhookAsync(webhookRequest);
            var secondSnapshot = await context.ServicePaymentTransactions
                .AsNoTracking()
                .SingleAsync(t => t.Id == seeded.TransactionId);

            Assert.True(secondResult.Success);
            Assert.Equal(PaymentTransactionStatus.Failed, secondResult.Status);
            Assert.Equal(firstSnapshot.UpdatedAt, secondSnapshot.UpdatedAt);
            Assert.Equal("evt_failed_001", secondSnapshot.ProviderEventId);
        }
    }

    [Fact(DisplayName = "Payment webhook servico sqlite integracao | Process webhook | Deve ignore stale transition quando incoming status tem lower priority")]
    public async Task ProcessWebhookAsync_ShouldIgnoreStaleTransition_WhenIncomingStatusHasLowerPriority()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var seeded = await SeedTransactionAsync(
                context,
                PaymentTransactionStatus.Paid,
                providerEventId: "evt_paid_previous");
            var before = await context.ServicePaymentTransactions
                .AsNoTracking()
                .SingleAsync(t => t.Id == seeded.TransactionId);

            var service = BuildService(context);
            var staleWebhookRequest = BuildWebhookRequest(
                seeded.ProviderTransactionId,
                status: "failed",
                eventId: "evt_failed_stale",
                amount: before.Amount,
                failureCode: "timeout");

            var result = await service.ProcessWebhookAsync(staleWebhookRequest);
            var after = await context.ServicePaymentTransactions
                .AsNoTracking()
                .SingleAsync(t => t.Id == seeded.TransactionId);

            Assert.True(result.Success);
            Assert.Equal(PaymentTransactionStatus.Paid, result.Status);
            Assert.Equal(PaymentTransactionStatus.Paid, after.Status);
            Assert.Equal("evt_paid_previous", after.ProviderEventId);
            Assert.Equal(before.UpdatedAt, after.UpdatedAt);
            Assert.Null(after.FailureCode);
            Assert.Null(after.FailureReason);
        }
    }

    [Fact(DisplayName = "Payment webhook servico sqlite integracao | Process webhook | Deve retornar invalido signature quando signature invalido")]
    public async Task ProcessWebhookAsync_ShouldReturnInvalidSignature_WhenSignatureIsInvalid()
    {
        var (context, connection) = InfrastructureTestDbContextFactory.CreateSqliteContext();
        using (connection)
        await using (context)
        {
            var seeded = await SeedTransactionAsync(context, PaymentTransactionStatus.Pending);
            var service = BuildService(context);

            var webhookRequest = BuildWebhookRequest(
                seeded.ProviderTransactionId,
                status: "paid",
                eventId: "evt_signature_invalid",
                signature: "wrong-secret");

            var result = await service.ProcessWebhookAsync(webhookRequest);
            var stored = await context.ServicePaymentTransactions
                .AsNoTracking()
                .SingleAsync(t => t.Id == seeded.TransactionId);

            Assert.False(result.Success);
            Assert.Equal("invalid_signature", result.ErrorCode);
            Assert.Equal(PaymentTransactionStatus.Pending, stored.Status);
            Assert.Null(stored.ProviderEventId);
        }
    }

    private static PaymentWebhookService BuildService(ConsertaPraMimDbContext context)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payments:Mock:WebhookSecret"] = "mock-secret",
                ["Payments:Mock:CheckoutBaseUrl"] = "https://mock.checkout.local",
                ["Payments:Mock:SessionExpiryMinutes"] = "30"
            })
            .Build();

        var paymentService = new MockPaymentService(
            NullLogger<MockPaymentService>.Instance,
            configuration);

        return new PaymentWebhookService(
            paymentService,
            new ServicePaymentTransactionRepository(context));
    }

    private static async Task<(Guid TransactionId, string ProviderTransactionId)> SeedTransactionAsync(
        ConsertaPraMimDbContext context,
        PaymentTransactionStatus status,
        string? providerEventId = null)
    {
        var client = new User
        {
            Name = "Cliente Webhook",
            Email = $"cliente.webhook.{Guid.NewGuid():N}@teste.com",
            PasswordHash = "hash",
            Phone = "11999990001",
            Role = UserRole.Client,
            IsActive = true
        };

        var provider = new User
        {
            Name = "Prestador Webhook",
            Email = $"prestador.webhook.{Guid.NewGuid():N}@teste.com",
            PasswordHash = "hash",
            Phone = "11999990002",
            Role = UserRole.Provider,
            IsActive = true
        };

        var request = new ServiceRequest
        {
            ClientId = client.Id,
            Category = ServiceCategory.Electrical,
            Status = ServiceRequestStatus.Completed,
            Description = "Pagamento de servico concluido",
            AddressStreet = "Rua Webhook",
            AddressCity = "Praia Grande",
            AddressZip = "11704150",
            Latitude = -24.01,
            Longitude = -46.41
        };

        var providerTransactionId = $"mock_txn_{Guid.NewGuid():N}";
        var transaction = new ServicePaymentTransaction
        {
            ServiceRequestId = request.Id,
            ClientId = client.Id,
            ProviderId = provider.Id,
            ProviderName = PaymentTransactionProvider.Mock,
            Method = PaymentTransactionMethod.Pix,
            Status = status,
            Amount = 99.90m,
            Currency = "BRL",
            CheckoutReference = $"chk_{Guid.NewGuid():N}",
            ProviderTransactionId = providerTransactionId,
            ProviderEventId = providerEventId,
            ProcessedAtUtc = status == PaymentTransactionStatus.Paid ? DateTime.UtcNow.AddMinutes(-2) : null,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        await context.Users.AddRangeAsync(client, provider);
        await context.ServiceRequests.AddAsync(request);
        await context.ServicePaymentTransactions.AddAsync(transaction);
        await context.SaveChangesAsync();

        return (transaction.Id, providerTransactionId);
    }

    private static PaymentWebhookRequestDto BuildWebhookRequest(
        string providerTransactionId,
        string status,
        string eventId,
        decimal amount = 99.90m,
        string currency = "BRL",
        string signature = "mock-secret",
        string? failureCode = null,
        string? failureReason = null)
    {
        var payload = JsonSerializer.Serialize(new
        {
            eventId,
            eventType = "payment.updated",
            providerTransactionId,
            status,
            amount,
            currency,
            occurredAtUtc = DateTime.UtcNow,
            failureCode,
            failureReason
        });

        return new PaymentWebhookRequestDto(
            PaymentTransactionProvider.Mock,
            payload,
            signature,
            eventId);
    }
}
