using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Entities;
using ConsertaPraMim.Domain.Enums;
using ConsertaPraMim.Domain.Repositories;

namespace ConsertaPraMim.Application.Services;

public class PaymentCheckoutService : IPaymentCheckoutService
{
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IUserRepository _userRepository;
    private readonly IServicePaymentTransactionRepository _paymentTransactionRepository;
    private readonly IPaymentService _paymentService;

    public PaymentCheckoutService(
        IServiceRequestRepository serviceRequestRepository,
        IUserRepository userRepository,
        IServicePaymentTransactionRepository paymentTransactionRepository,
        IPaymentService paymentService)
    {
        _serviceRequestRepository = serviceRequestRepository;
        _userRepository = userRepository;
        _paymentTransactionRepository = paymentTransactionRepository;
        _paymentService = paymentService;
    }

    public async Task<PaymentCheckoutResultDto> CreateCheckoutAsync(
        Guid actorUserId,
        string actorRole,
        CreatePaymentCheckoutRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.ServiceRequestId == Guid.Empty)
        {
            return new PaymentCheckoutResultDto(
                false,
                ErrorCode: "invalid_request",
                ErrorMessage: "Pedido de servico invalido.");
        }

        if (!IsClientRole(actorRole) && !IsAdminRole(actorRole))
        {
            return new PaymentCheckoutResultDto(
                false,
                ErrorCode: "forbidden",
                ErrorMessage: "Somente cliente (ou admin) pode iniciar checkout.");
        }

        var serviceRequest = await _serviceRequestRepository.GetByIdAsync(request.ServiceRequestId);
        if (serviceRequest == null)
        {
            return new PaymentCheckoutResultDto(
                false,
                ErrorCode: "request_not_found",
                ErrorMessage: "Pedido de servico nao encontrado.");
        }

        if (!IsAdminRole(actorRole) && serviceRequest.ClientId != actorUserId)
        {
            return new PaymentCheckoutResultDto(
                false,
                ErrorCode: "forbidden",
                ErrorMessage: "Cliente sem permissao para pagar este pedido.");
        }

        if (serviceRequest.Status != ServiceRequestStatus.Completed)
        {
            return new PaymentCheckoutResultDto(
                false,
                ErrorCode: "invalid_state",
                ErrorMessage: "Pagamento disponivel apenas para pedidos concluidos.");
        }

        var normalizedCurrency = NormalizeCurrency(request.Currency);
        var parsedMethod = ParseMethod(request.Method);
        if (parsedMethod == null)
        {
            return new PaymentCheckoutResultDto(
                false,
                ErrorCode: "invalid_method",
                ErrorMessage: "Metodo de pagamento invalido. Use Pix ou Card.");
        }

        var providerId = ResolveProviderId(serviceRequest, request.ProviderId);
        if (providerId == null)
        {
            return new PaymentCheckoutResultDto(
                false,
                ErrorCode: "provider_required",
                ErrorMessage: "Informe o prestador para pagamento quando houver mais de um candidato.");
        }

        var provider = await _userRepository.GetByIdAsync(providerId.Value);
        if (provider == null || provider.Role != UserRole.Provider || !provider.IsActive)
        {
            return new PaymentCheckoutResultDto(
                false,
                ErrorCode: "provider_not_found",
                ErrorMessage: "Prestador nao encontrado para pagamento.");
        }

        var amount = ResolveAmount(serviceRequest, providerId.Value);
        if (amount <= 0)
        {
            return new PaymentCheckoutResultDto(
                false,
                ErrorCode: "invalid_amount",
                ErrorMessage: "Nao foi possivel determinar o valor do pagamento.");
        }

        var session = await _paymentService.CreateCheckoutSessionAsync(
            new PaymentCheckoutRequestDto(
                ServiceRequestId: serviceRequest.Id,
                ClientId: serviceRequest.ClientId,
                ProviderId: providerId.Value,
                Amount: amount,
                Method: parsedMethod.Value,
                Currency: normalizedCurrency,
                SuccessUrl: request.SuccessUrl,
                FailureUrl: request.FailureUrl,
                WebhookNotificationUrl: request.WebhookNotificationUrl,
                IdempotencyKey: request.IdempotencyKey,
                Metadata: request.Metadata),
            cancellationToken);

        var transaction = new ServicePaymentTransaction
        {
            ServiceRequestId = serviceRequest.Id,
            ClientId = serviceRequest.ClientId,
            ProviderId = providerId.Value,
            ProviderName = session.Provider,
            Method = parsedMethod.Value,
            Status = session.Status,
            Amount = amount,
            Currency = normalizedCurrency,
            CheckoutReference = session.CheckoutReference,
            ProviderTransactionId = session.ProviderTransactionId,
            ExpiresAtUtc = session.ExpiresAtUtc,
            ProcessedAtUtc = session.Status == PaymentTransactionStatus.Paid ? DateTime.UtcNow : null,
            RefundedAtUtc = session.Status == PaymentTransactionStatus.Refunded ? DateTime.UtcNow : null,
            MetadataJson = BuildMetadataJson(request)
        };

        await _paymentTransactionRepository.AddAsync(transaction);

        return new PaymentCheckoutResultDto(
            Success: true,
            TransactionId: transaction.Id,
            ServiceRequestId: serviceRequest.Id,
            ProviderId: providerId.Value,
            Amount: amount,
            Currency: normalizedCurrency,
            Method: parsedMethod.Value,
            Session: session);
    }

    private static Guid? ResolveProviderId(ServiceRequest serviceRequest, Guid? requestedProviderId)
    {
        if (requestedProviderId.HasValue && requestedProviderId.Value != Guid.Empty)
        {
            return requestedProviderId.Value;
        }

        var acceptedProviders = serviceRequest.Proposals
            .Where(p => p.Accepted && !p.IsInvalidated)
            .Select(p => p.ProviderId)
            .Distinct()
            .ToList();

        if (acceptedProviders.Count == 1)
        {
            return acceptedProviders[0];
        }

        var completedAppointmentProviders = serviceRequest.Appointments
            .Where(a => a.Status == ServiceAppointmentStatus.Completed)
            .Select(a => a.ProviderId)
            .Distinct()
            .ToList();

        return completedAppointmentProviders.Count == 1
            ? completedAppointmentProviders[0]
            : null;
    }

    private static decimal ResolveAmount(ServiceRequest serviceRequest, Guid providerId)
    {
        if (serviceRequest.CommercialCurrentValue.HasValue && serviceRequest.CommercialCurrentValue.Value > 0m)
        {
            return decimal.Round(serviceRequest.CommercialCurrentValue.Value, 2, MidpointRounding.AwayFromZero);
        }

        var acceptedProposal = serviceRequest.Proposals
            .Where(p => p.ProviderId == providerId && p.Accepted && !p.IsInvalidated)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefault();

        var estimatedValue = acceptedProposal?.EstimatedValue;
        if (estimatedValue.HasValue && estimatedValue.Value > 0m)
        {
            return decimal.Round(estimatedValue.Value, 2, MidpointRounding.AwayFromZero);
        }

        return 0m;
    }

    private static PaymentTransactionMethod? ParseMethod(string? method)
    {
        var normalized = method?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "pix" => PaymentTransactionMethod.Pix,
            "card" => PaymentTransactionMethod.Card,
            "cartao" => PaymentTransactionMethod.Card,
            _ => null
        };
    }

    private static bool IsClientRole(string role) =>
        role.Equals(UserRole.Client.ToString(), StringComparison.OrdinalIgnoreCase);

    private static bool IsAdminRole(string role) =>
        role.Equals(UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return "BRL";
        }

        var normalized = currency.Trim().ToUpperInvariant();
        return normalized.Length <= 8 ? normalized : normalized[..8];
    }

    private static string? BuildMetadataJson(CreatePaymentCheckoutRequestDto request)
    {
        if (request.Metadata == null || request.Metadata.Count == 0)
        {
            return null;
        }

        var normalized = request.Metadata
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
            .ToDictionary(
                kv => kv.Key.Trim(),
                kv => kv.Value ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        if (normalized.Count == 0)
        {
            return null;
        }

        return System.Text.Json.JsonSerializer.Serialize(normalized);
    }
}
