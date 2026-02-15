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

public record CreatePaymentCheckoutRequestDto(
    Guid ServiceRequestId,
    string Method,
    Guid? ProviderId = null,
    string Currency = "BRL",
    string? SuccessUrl = null,
    string? FailureUrl = null,
    string? WebhookNotificationUrl = null,
    string? IdempotencyKey = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public record PaymentCheckoutResultDto(
    bool Success,
    Guid? TransactionId = null,
    Guid? ServiceRequestId = null,
    Guid? ProviderId = null,
    decimal? Amount = null,
    string? Currency = null,
    PaymentTransactionMethod? Method = null,
    PaymentCheckoutSessionDto? Session = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record PaymentWebhookProcessResultDto(
    bool Success,
    Guid? TransactionId = null,
    string? ProviderTransactionId = null,
    PaymentTransactionStatus? Status = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record PaymentReceiptDto(
    Guid TransactionId,
    Guid ServiceRequestId,
    Guid ClientId,
    string ClientName,
    Guid ProviderId,
    string ProviderName,
    decimal Amount,
    string Currency,
    string Method,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc,
    DateTime? RefundedAtUtc,
    DateTime? ExpiresAtUtc,
    string ProviderTransactionId,
    string CheckoutReference,
    string ReceiptNumber,
    string? ReceiptUrl);

public record PaymentReceiptResultDto(
    bool Success,
    PaymentReceiptDto? Receipt = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);
