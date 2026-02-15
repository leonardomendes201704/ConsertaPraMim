using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class PaymentReceiptService : IPaymentReceiptService
{
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IServicePaymentTransactionRepository _paymentTransactionRepository;

    public PaymentReceiptService(
        IServiceRequestRepository serviceRequestRepository,
        IServicePaymentTransactionRepository paymentTransactionRepository)
    {
        _serviceRequestRepository = serviceRequestRepository;
        _paymentTransactionRepository = paymentTransactionRepository;
    }

    public async Task<IReadOnlyList<PaymentReceiptDto>> GetByServiceRequestAsync(
        Guid actorUserId,
        string actorRole,
        Guid serviceRequestId,
        CancellationToken cancellationToken = default)
    {
        if (serviceRequestId == Guid.Empty)
        {
            return Array.Empty<PaymentReceiptDto>();
        }

        var request = await _serviceRequestRepository.GetByIdAsync(serviceRequestId);
        if (request == null || !HasAccess(request, actorUserId, actorRole))
        {
            return Array.Empty<PaymentReceiptDto>();
        }

        var transactions = await _paymentTransactionRepository.GetByServiceRequestIdAsync(serviceRequestId);
        return transactions
            .OrderByDescending(t => t.ProcessedAtUtc ?? t.CreatedAt)
            .ThenByDescending(t => t.CreatedAt)
            .Select(MapReceipt)
            .ToList();
    }

    public async Task<PaymentReceiptResultDto> GetByTransactionAsync(
        Guid actorUserId,
        string actorRole,
        Guid serviceRequestId,
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        if (serviceRequestId == Guid.Empty)
        {
            return new PaymentReceiptResultDto(
                false,
                ErrorCode: "request_not_found",
                ErrorMessage: "Pedido de servico nao informado.");
        }

        if (transactionId == Guid.Empty)
        {
            return new PaymentReceiptResultDto(
                false,
                ErrorCode: "transaction_not_found",
                ErrorMessage: "Transacao de pagamento nao informada.");
        }

        var request = await _serviceRequestRepository.GetByIdAsync(serviceRequestId);
        if (request == null)
        {
            return new PaymentReceiptResultDto(
                false,
                ErrorCode: "request_not_found",
                ErrorMessage: "Pedido de servico nao encontrado.");
        }

        if (!HasAccess(request, actorUserId, actorRole))
        {
            return new PaymentReceiptResultDto(
                false,
                ErrorCode: "forbidden",
                ErrorMessage: "Usuario sem permissao para visualizar este comprovante.");
        }

        var transaction = await _paymentTransactionRepository.GetByIdAsync(transactionId);
        if (transaction == null || transaction.ServiceRequestId != serviceRequestId)
        {
            return new PaymentReceiptResultDto(
                false,
                ErrorCode: "transaction_not_found",
                ErrorMessage: "Transacao de pagamento nao encontrada para este pedido.");
        }

        return new PaymentReceiptResultDto(
            true,
            Receipt: MapReceipt(transaction));
    }

    private static bool HasAccess(ServiceRequest request, Guid actorUserId, string actorRole)
    {
        if (actorUserId == Guid.Empty || string.IsNullOrWhiteSpace(actorRole))
        {
            return false;
        }

        if (actorRole.Equals(UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (actorRole.Equals(UserRole.Client.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return request.ClientId == actorUserId;
        }

        if (!actorRole.Equals(UserRole.Provider.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var linkedByProposal = request.Proposals.Any(p =>
            p.ProviderId == actorUserId && !p.IsInvalidated);

        var linkedByAppointment = request.Appointments.Any(a =>
            a.ProviderId == actorUserId);

        return linkedByProposal || linkedByAppointment;
    }

    private static PaymentReceiptDto MapReceipt(ServicePaymentTransaction transaction)
    {
        var receiptNumber = string.IsNullOrWhiteSpace(transaction.ReceiptNumber)
            ? BuildFallbackReceiptNumber(transaction)
            : transaction.ReceiptNumber!.Trim();

        return new PaymentReceiptDto(
            TransactionId: transaction.Id,
            ServiceRequestId: transaction.ServiceRequestId,
            ClientId: transaction.ClientId,
            ClientName: transaction.Client?.Name ?? "Cliente",
            ProviderId: transaction.ProviderId,
            ProviderName: transaction.Provider?.Name ?? "Prestador",
            Amount: decimal.Round(transaction.Amount, 2, MidpointRounding.AwayFromZero),
            Currency: string.IsNullOrWhiteSpace(transaction.Currency) ? "BRL" : transaction.Currency.Trim().ToUpperInvariant(),
            Method: transaction.Method.ToString(),
            Status: transaction.Status.ToString(),
            CreatedAtUtc: transaction.CreatedAt,
            ProcessedAtUtc: transaction.ProcessedAtUtc,
            RefundedAtUtc: transaction.RefundedAtUtc,
            ExpiresAtUtc: transaction.ExpiresAtUtc,
            ProviderTransactionId: transaction.ProviderTransactionId,
            CheckoutReference: transaction.CheckoutReference,
            ReceiptNumber: receiptNumber,
            ReceiptUrl: transaction.ReceiptUrl);
    }

    private static string BuildFallbackReceiptNumber(ServicePaymentTransaction transaction)
    {
        var idChunk = transaction.Id.ToString("N")[..8].ToUpperInvariant();
        var dateChunk = (transaction.ProcessedAtUtc ?? transaction.CreatedAt).ToUniversalTime().ToString("yyyyMMdd");
        return $"CPM-{dateChunk}-{idChunk}";
    }
}
