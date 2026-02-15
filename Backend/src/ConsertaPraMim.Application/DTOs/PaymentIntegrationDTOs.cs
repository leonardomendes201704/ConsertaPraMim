using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.DTOs;

public record PaymentCheckoutRequestDto(
    Guid ServiceRequestId,
    Guid ClientId,
    Guid ProviderId,
    decimal Amount,
    PaymentTransactionMethod Method,
    string Currency = "BRL",
    string? SuccessUrl = null,
    string? FailureUrl = null,
    string? WebhookNotificationUrl = null,
    string? IdempotencyKey = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public record PaymentCheckoutSessionDto(
    PaymentTransactionProvider Provider,
    string CheckoutReference,
    string CheckoutUrl,
    string ProviderTransactionId,
    PaymentTransactionStatus Status,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc);

public record PaymentWebhookRequestDto(
    PaymentTransactionProvider Provider,
    string RawBody,
    string Signature,
    string? EventId = null);

public record PaymentWebhookEventDto(
    string EventId,
    string EventType,
    string ProviderTransactionId,
    PaymentTransactionStatus Status,
    decimal Amount,
    string Currency,
    DateTime OccurredAtUtc,
    string? FailureCode = null,
    string? FailureReason = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public record PaymentRefundRequestDto(
    string ProviderTransactionId,
    decimal? Amount = null,
    string? Reason = null,
    string? IdempotencyKey = null);
