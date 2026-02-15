using System.Globalization;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ConsertaPraMim.Infrastructure.Services;

public class MockPaymentService : IPaymentService
{
    private readonly ILogger<MockPaymentService> _logger;
    private readonly string _checkoutBaseUrl;
    private readonly string _webhookSecret;
    private readonly int _sessionExpiryMinutes;

    public MockPaymentService(ILogger<MockPaymentService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _checkoutBaseUrl = string.IsNullOrWhiteSpace(configuration["Payments:Mock:CheckoutBaseUrl"])
            ? "https://checkout.consertapramim.com/pay"
            : configuration["Payments:Mock:CheckoutBaseUrl"]!.TrimEnd('/');
        _webhookSecret = string.IsNullOrWhiteSpace(configuration["Payments:Mock:WebhookSecret"])
            ? "mock-secret"
            : configuration["Payments:Mock:WebhookSecret"]!;
        _sessionExpiryMinutes = ParseIntPolicyValue(configuration["Payments:Mock:SessionExpiryMinutes"], 30, 5, 240);
    }

    public Task<PaymentCheckoutSessionDto> CreateCheckoutSessionAsync(
        PaymentCheckoutRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var amount = decimal.Round(Math.Max(0m, request.Amount), 2, MidpointRounding.AwayFromZero);
        var checkoutReference = $"mock_{request.ServiceRequestId:N}_{Guid.NewGuid():N}";
        var providerTransactionId = $"mock_txn_{Guid.NewGuid():N}";
        var methodToken = request.Method == PaymentTransactionMethod.Pix ? "pix" : "card";
        var amountToken = amount.ToString("F2", CultureInfo.InvariantCulture);
        var checkoutUrl = $"{_checkoutBaseUrl}/{methodToken}/{checkoutReference}?amount={amountToken}&currency={request.Currency}";

        _logger.LogInformation(
            "MOCK PAYMENT: Created checkout session. RequestId={ServiceRequestId} Method={Method} Amount={Amount} Currency={Currency} CheckoutReference={CheckoutReference}",
            request.ServiceRequestId,
            request.Method,
            amount,
            request.Currency,
            checkoutReference);

        var session = new PaymentCheckoutSessionDto(
            Provider: PaymentTransactionProvider.Mock,
            CheckoutReference: checkoutReference,
            CheckoutUrl: checkoutUrl,
            ProviderTransactionId: providerTransactionId,
            Status: PaymentTransactionStatus.Pending,
            CreatedAtUtc: nowUtc,
            ExpiresAtUtc: nowUtc.AddMinutes(_sessionExpiryMinutes));

        return Task.FromResult(session);
    }

    public bool ValidateWebhookSignature(PaymentWebhookRequestDto request)
    {
        if (request.Provider != PaymentTransactionProvider.Mock)
        {
            return false;
        }

        return string.Equals(request.Signature, _webhookSecret, StringComparison.Ordinal);
    }

    public Task<PaymentWebhookEventDto?> ParseWebhookAsync(
        PaymentWebhookRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!ValidateWebhookSignature(request))
        {
            _logger.LogWarning("MOCK PAYMENT: Invalid webhook signature.");
            return Task.FromResult<PaymentWebhookEventDto?>(null);
        }

        try
        {
            var payload = JsonSerializer.Deserialize<MockWebhookPayload>(
                request.RawBody,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (payload == null || string.IsNullOrWhiteSpace(payload.ProviderTransactionId))
            {
                return Task.FromResult<PaymentWebhookEventDto?>(null);
            }

            var parsedStatus = ParseStatus(payload.Status);
            var occurredAtUtc = payload.OccurredAtUtc == default ? DateTime.UtcNow : payload.OccurredAtUtc.ToUniversalTime();
            var eventId = string.IsNullOrWhiteSpace(payload.EventId) ? (request.EventId ?? Guid.NewGuid().ToString("N")) : payload.EventId;
            var eventType = string.IsNullOrWhiteSpace(payload.EventType) ? "payment.updated" : payload.EventType;

            var evt = new PaymentWebhookEventDto(
                EventId: eventId,
                EventType: eventType,
                ProviderTransactionId: payload.ProviderTransactionId,
                Status: parsedStatus,
                Amount: decimal.Round(Math.Max(0m, payload.Amount), 2, MidpointRounding.AwayFromZero),
                Currency: string.IsNullOrWhiteSpace(payload.Currency) ? "BRL" : payload.Currency.ToUpperInvariant(),
                OccurredAtUtc: occurredAtUtc,
                FailureCode: payload.FailureCode,
                FailureReason: payload.FailureReason,
                Metadata: payload.Metadata);

            return Task.FromResult<PaymentWebhookEventDto?>(evt);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "MOCK PAYMENT: Invalid webhook payload.");
            return Task.FromResult<PaymentWebhookEventDto?>(null);
        }
    }

    public Task<bool> RefundAsync(
        PaymentRefundRequestDto request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "MOCK PAYMENT: Refunding transaction {ProviderTransactionId}. Amount={Amount}",
            request.ProviderTransactionId,
            request.Amount);
        return Task.FromResult(true);
    }

    public Task<bool> ReleaseFundsAsync(
        Guid serviceRequestId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK PAYMENT: Releasing funds for service request {ServiceRequestId}.", serviceRequestId);
        return Task.FromResult(true);
    }

    private static PaymentTransactionStatus ParseStatus(string? rawStatus)
    {
        var normalized = (rawStatus ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "paid" => PaymentTransactionStatus.Paid,
            "failed" => PaymentTransactionStatus.Failed,
            "refunded" => PaymentTransactionStatus.Refunded,
            _ => PaymentTransactionStatus.Pending
        };
    }

    private static int ParseIntPolicyValue(string? raw, int defaultValue, int minimum, int maximum)
    {
        if (!int.TryParse(raw, out var parsed))
        {
            return defaultValue;
        }

        return Math.Clamp(parsed, minimum, maximum);
    }

    private sealed class MockWebhookPayload
    {
        public string? EventId { get; set; }
        public string? EventType { get; set; }
        public string ProviderTransactionId { get; set; } = string.Empty;
        public string? Status { get; set; }
        public decimal Amount { get; set; }
        public string? Currency { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public string? FailureCode { get; set; }
        public string? FailureReason { get; set; }
        public IReadOnlyDictionary<string, string>? Metadata { get; set; }
    }
}
