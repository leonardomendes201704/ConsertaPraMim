using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class PaymentWebhookService : IPaymentWebhookService
{
    private readonly IPaymentService _paymentService;
    private readonly IServicePaymentTransactionRepository _paymentTransactionRepository;

    public PaymentWebhookService(
        IPaymentService paymentService,
        IServicePaymentTransactionRepository paymentTransactionRepository)
    {
        _paymentService = paymentService;
        _paymentTransactionRepository = paymentTransactionRepository;
    }

    public async Task<PaymentWebhookProcessResultDto> ProcessWebhookAsync(
        PaymentWebhookRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!_paymentService.ValidateWebhookSignature(request))
        {
            return new PaymentWebhookProcessResultDto(
                false,
                ErrorCode: "invalid_signature",
                ErrorMessage: "Assinatura do webhook invalida.");
        }

        var webhookEvent = await _paymentService.ParseWebhookAsync(request, cancellationToken);
        if (webhookEvent == null || string.IsNullOrWhiteSpace(webhookEvent.ProviderTransactionId))
        {
            return new PaymentWebhookProcessResultDto(
                false,
                ErrorCode: "invalid_payload",
                ErrorMessage: "Payload de webhook invalido.");
        }

        var transaction = await _paymentTransactionRepository.GetByProviderTransactionIdAsync(webhookEvent.ProviderTransactionId);
        if (transaction == null)
        {
            return new PaymentWebhookProcessResultDto(
                false,
                ProviderTransactionId: webhookEvent.ProviderTransactionId,
                ErrorCode: "transaction_not_found",
                ErrorMessage: "Transacao nao encontrada para o providerTransactionId informado.");
        }

        transaction.Status = webhookEvent.Status;
        transaction.ProviderEventId = webhookEvent.EventId;
        transaction.FailureCode = NormalizeNullable(webhookEvent.FailureCode, 80);
        transaction.FailureReason = NormalizeNullable(webhookEvent.FailureReason, 500);
        transaction.Currency = NormalizeCurrency(webhookEvent.Currency);
        transaction.Amount = webhookEvent.Amount > 0
            ? decimal.Round(webhookEvent.Amount, 2, MidpointRounding.AwayFromZero)
            : transaction.Amount;
        transaction.MetadataJson = BuildMetadataJson(webhookEvent.Metadata);

        if (webhookEvent.Status == PaymentTransactionStatus.Paid)
        {
            transaction.ProcessedAtUtc = NormalizeOccurredAt(webhookEvent.OccurredAtUtc);
            transaction.RefundedAtUtc = null;
        }
        else if (webhookEvent.Status == PaymentTransactionStatus.Refunded)
        {
            transaction.RefundedAtUtc = NormalizeOccurredAt(webhookEvent.OccurredAtUtc);
            transaction.ProcessedAtUtc ??= transaction.RefundedAtUtc;
        }
        else if (webhookEvent.Status == PaymentTransactionStatus.Failed)
        {
            transaction.ProcessedAtUtc ??= NormalizeOccurredAt(webhookEvent.OccurredAtUtc);
        }

        transaction.UpdatedAt = DateTime.UtcNow;
        await _paymentTransactionRepository.UpdateAsync(transaction);

        return new PaymentWebhookProcessResultDto(
            true,
            TransactionId: transaction.Id,
            ProviderTransactionId: transaction.ProviderTransactionId,
            Status: transaction.Status);
    }

    private static DateTime NormalizeOccurredAt(DateTime occurredAtUtc)
    {
        if (occurredAtUtc == default)
        {
            return DateTime.UtcNow;
        }

        return occurredAtUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc)
            : occurredAtUtc.ToUniversalTime();
    }

    private static string NormalizeCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return "BRL";
        }

        var normalized = currency.Trim().ToUpperInvariant();
        return normalized.Length <= 8 ? normalized : normalized[..8];
    }

    private static string? NormalizeNullable(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? BuildMetadataJson(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata == null || metadata.Count == 0)
        {
            return null;
        }

        var normalized = metadata
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
            .ToDictionary(
                kv => kv.Key.Trim(),
                kv => kv.Value ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        if (normalized.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(normalized);
    }
}
