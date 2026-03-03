using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Application.Interfaces;
using ConsertaPraMim.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConsertaPraMim.API.Controllers;

[Authorize]
[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentCheckoutService _paymentCheckoutService;
    private readonly IPaymentWebhookService _paymentWebhookService;
    private readonly IPaymentReceiptService _paymentReceiptService;

    public PaymentsController(
        IPaymentCheckoutService paymentCheckoutService,
        IPaymentWebhookService paymentWebhookService,
        IPaymentReceiptService paymentReceiptService)
    {
        _paymentCheckoutService = paymentCheckoutService;
        _paymentWebhookService = paymentWebhookService;
        _paymentReceiptService = paymentReceiptService;
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckout([FromBody] CreatePaymentCheckoutRequestDto request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _paymentCheckoutService.CreateCheckoutAsync(actorUserId, actorRole, request, cancellationToken);
        if (result.Success)
        {
            return Ok(result);
        }

        return result.ErrorCode switch
        {
            "forbidden" => Forbid(),
            "request_not_found" => NotFound(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            "provider_not_found" => NotFound(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            "invalid_state" => Conflict(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            "provider_required" => BadRequest(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            "invalid_method" => BadRequest(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            "invalid_amount" => BadRequest(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            "invalid_request" => BadRequest(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            _ => BadRequest(new { errorCode = result.ErrorCode, message = result.ErrorMessage })
        };
    }

    [HttpGet("requests/{serviceRequestId:guid}/receipts")]
    public async Task<IActionResult> GetReceiptsByServiceRequest(Guid serviceRequestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var receipts = await _paymentReceiptService.GetByServiceRequestAsync(
            actorUserId,
            actorRole,
            serviceRequestId,
            cancellationToken);

        return Ok(receipts);
    }

    [HttpGet("requests/{serviceRequestId:guid}/receipts/{transactionId:guid}")]
    public async Task<IActionResult> GetReceiptByTransaction(Guid serviceRequestId, Guid transactionId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        var result = await _paymentReceiptService.GetByTransactionAsync(
            actorUserId,
            actorRole,
            serviceRequestId,
            transactionId,
            cancellationToken);

        if (result.Success && result.Receipt != null)
        {
            return Ok(result.Receipt);
        }

        return result.ErrorCode switch
        {
            "forbidden" => Forbid(),
            "request_not_found" => NotFound(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            "transaction_not_found" => NotFound(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            _ => BadRequest(new { errorCode = result.ErrorCode, message = result.ErrorMessage })
        };
    }

    [AllowAnonymous]
    [HttpPost("webhook/{provider}")]
    public async Task<IActionResult> ReceiveWebhook(string provider, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PaymentTransactionProvider>(provider, ignoreCase: true, out var paymentProvider))
        {
            return BadRequest(new { errorCode = "invalid_provider", message = "Provider de pagamento invalido." });
        }

        Request.EnableBuffering();
        string rawBody;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
        {
            rawBody = await reader.ReadToEndAsync(cancellationToken);
        }
        Request.Body.Position = 0;

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return BadRequest(new { errorCode = "invalid_payload", message = "Payload do webhook nao informado." });
        }

        var signature =
            Request.Headers["X-Payment-Signature"].FirstOrDefault() ??
            Request.Headers["X-Webhook-Signature"].FirstOrDefault() ??
            Request.Headers["X-Mock-Signature"].FirstOrDefault() ??
            string.Empty;

        var eventId =
            Request.Headers["X-Payment-Event-Id"].FirstOrDefault() ??
            Request.Headers["X-Event-Id"].FirstOrDefault();

        var result = await _paymentWebhookService.ProcessWebhookAsync(
            new PaymentWebhookRequestDto(
                paymentProvider,
                rawBody,
                signature,
                eventId),
            cancellationToken);

        if (result.Success)
        {
            return Accepted(result);
        }

        return result.ErrorCode switch
        {
            "invalid_signature" => Unauthorized(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            "invalid_payload" => BadRequest(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            "transaction_not_found" => NotFound(new { errorCode = result.ErrorCode, message = result.ErrorMessage }),
            _ => BadRequest(new { errorCode = result.ErrorCode, message = result.ErrorMessage })
        };
    }

    [Authorize(Roles = "Client,Admin")]
    [HttpPost("simulate/mock")]
    public async Task<IActionResult> SimulateMockWebhook(
        [FromBody] SimulateMockPaymentRequestDto request,
        [FromServices] IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole))
        {
            return Unauthorized();
        }

        if (request.ServiceRequestId == Guid.Empty || request.TransactionId == Guid.Empty)
        {
            return BadRequest(new { errorCode = "invalid_request", message = "Transacao de pagamento invalida." });
        }

        if (!TryNormalizeSimulatedStatus(request.Status, out var normalizedStatus))
        {
            return BadRequest(new { errorCode = "invalid_status", message = "Status simulado invalido. Use paid ou failed." });
        }

        var receiptResult = await _paymentReceiptService.GetByTransactionAsync(
            actorUserId,
            actorRole,
            request.ServiceRequestId,
            request.TransactionId,
            cancellationToken);

        if (!receiptResult.Success || receiptResult.Receipt == null)
        {
            return receiptResult.ErrorCode switch
            {
                "forbidden" => Forbid(),
                "request_not_found" => NotFound(new { errorCode = receiptResult.ErrorCode, message = receiptResult.ErrorMessage }),
                "transaction_not_found" => NotFound(new { errorCode = receiptResult.ErrorCode, message = receiptResult.ErrorMessage }),
                _ => BadRequest(new { errorCode = receiptResult.ErrorCode, message = receiptResult.ErrorMessage })
            };
        }

        var payload = JsonSerializer.Serialize(new
        {
            eventId = $"mock_evt_{Guid.NewGuid():N}",
            eventType = "payment.updated",
            providerTransactionId = receiptResult.Receipt.ProviderTransactionId,
            status = normalizedStatus,
            amount = receiptResult.Receipt.Amount,
            currency = receiptResult.Receipt.Currency,
            occurredAtUtc = DateTime.UtcNow
        });

        var signature = string.IsNullOrWhiteSpace(configuration["Payments:Mock:WebhookSecret"])
            ? "mock-secret"
            : configuration["Payments:Mock:WebhookSecret"]!;

        var webhookResult = await _paymentWebhookService.ProcessWebhookAsync(
            new PaymentWebhookRequestDto(
                PaymentTransactionProvider.Mock,
                payload,
                signature,
                EventId: null),
            cancellationToken);

        if (!webhookResult.Success)
        {
            return webhookResult.ErrorCode switch
            {
                "invalid_signature" => Unauthorized(new { errorCode = webhookResult.ErrorCode, message = webhookResult.ErrorMessage }),
                "transaction_not_found" => NotFound(new { errorCode = webhookResult.ErrorCode, message = webhookResult.ErrorMessage }),
                _ => BadRequest(new { errorCode = webhookResult.ErrorCode, message = webhookResult.ErrorMessage })
            };
        }

        return Ok(new
        {
            success = true,
            transactionId = webhookResult.TransactionId,
            providerTransactionId = webhookResult.ProviderTransactionId,
            status = webhookResult.Status?.ToString()
        });
    }

    private bool TryGetActor(out Guid actorUserId, out string actorRole)
    {
        actorUserId = Guid.Empty;
        actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

        var actorRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(actorRaw) && Guid.TryParse(actorRaw, out actorUserId);
    }

    private static bool TryNormalizeSimulatedStatus(string? status, out string normalizedStatus)
    {
        normalizedStatus = (status ?? string.Empty).Trim().ToLowerInvariant();
        return normalizedStatus is "paid" or "failed";
    }
}
