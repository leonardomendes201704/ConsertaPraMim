using ConsertaPraMim.Domain.Common;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Domain.Entities;

public class ServicePaymentTransaction : BaseEntity
{
    public Guid ServiceRequestId { get; set; }
    public ServiceRequest ServiceRequest { get; set; } = null!;

    public Guid ClientId { get; set; }
    public User Client { get; set; } = null!;

    public Guid ProviderId { get; set; }
    public User Provider { get; set; } = null!;

    public PaymentTransactionProvider ProviderName { get; set; } = PaymentTransactionProvider.Mock;
    public PaymentTransactionMethod Method { get; set; }
    public PaymentTransactionStatus Status { get; set; } = PaymentTransactionStatus.Pending;

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BRL";

    public string CheckoutReference { get; set; } = string.Empty;
    public string ProviderTransactionId { get; set; } = string.Empty;
    public string? ProviderEventId { get; set; }

    public string? FailureCode { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public DateTime? RefundedAtUtc { get; set; }

    public string? ReceiptNumber { get; set; }
    public string? ReceiptUrl { get; set; }
    public string? MetadataJson { get; set; }
}
