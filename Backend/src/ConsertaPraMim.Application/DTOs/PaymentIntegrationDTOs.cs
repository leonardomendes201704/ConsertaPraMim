namespace ConsertaPraMim.Application.DTOs;

public enum PaymentProvider
{
    Mock = 1
}

public enum PaymentMethod
{
    Pix = 1,
    Card = 2
}

public enum PaymentTransactionStatus
{
    Pending = 1,
    Paid = 2,
    Failed = 3,
    Refunded = 4
}

public record PaymentCheckoutRequestDto(
    Guid ServiceRequestId,
    Guid ClientId,
    Guid ProviderId,
    decimal Amount,
    PaymentMethod Method,
    string Currency = "BRL",
    string? SuccessUrl = null,
    string? FailureUrl = null,
    string? WebhookNotificationUrl = null,
    string? IdempotencyKey = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public record PaymentCheckoutSessionDto(
    PaymentProvider Provider,
    string CheckoutReference,
    string CheckoutUrl,
    string ProviderTransactionId,
    PaymentTransactionStatus Status,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc);

public record PaymentWebhookRequestDto(
    PaymentProvider Provider,
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
